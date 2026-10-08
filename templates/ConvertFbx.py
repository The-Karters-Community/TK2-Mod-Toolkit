"""Run with Blender --background --factory-startup --disable-autoexec --python ... -- input.fbx output/model.obj.

Converts a creator model to a static cosmetic shell. Script execution from files is disabled;
Blender is an installed converter, not a filesystem sandbox. Images are restricted to staged inputs.
"""
from pathlib import Path
import json
import math
import sys
import bpy


def convert(source: Path, target: Path):
    source, target = source.resolve(), target.resolve()
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    warnings = ["FBX is imported as a static kart shell; animation and driver rigs are not replaced."]
    result = bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=False, use_anim=False, use_custom_props=False)
    if "FINISHED" not in result:
        raise RuntimeError("Blender could not import this FBX. Blender's FBX importer expects binary FBX.")
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if not meshes:
        raise RuntimeError("FBX contains no mesh objects.")
    if len(meshes) > 64:
        raise RuntimeError("FBX contains more than 64 mesh objects; simplify the model first.")
    depsgraph = bpy.context.evaluated_depsgraph_get()
    vertices = triangles = 0
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            vertices += len(mesh.vertices)
            triangles += len(mesh.loop_triangles)
            if any(not math.isfinite(v) or abs(v) > 1000000 for row in obj.matrix_world for v in row):
                raise RuntimeError("FBX contains invalid object transforms.")
            if any(not math.isfinite(v) or abs(v) > 1000000 for vert in mesh.vertices for v in vert.co):
                raise RuntimeError("FBX contains invalid vertex coordinates.")
        finally:
            evaluated.to_mesh_clear()
    if vertices > 250000 or triangles > 500000:
        raise RuntimeError("FBX exceeds 250,000 vertices or 500,000 triangles; decimate it before importing.")
    # FBX may contain absolute image paths from its creator. Resolve by filename against
    # staged images only. No material export may read from the creator's arbitrary path.
    staged = {}
    for file in source.parent.rglob("*"):
        if file.is_file() and file.suffix.lower() in {".png", ".jpg", ".jpeg"}:
            staged.setdefault(file.name.casefold(), file.resolve())
    converted = {}
    images = {node.image for material in bpy.data.materials if material.node_tree is not None
              for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image is not None}
    for image in images:
        filename = Path(bpy.path.abspath(image.filepath)).name
        candidate = staged.get(filename.casefold())
        if candidate is not None:
            safe_image = bpy.data.images.load(str(candidate), check_existing=False)
        elif image.packed_file is not None:
            safe_image = image
        else:
            warnings.append("Missing diffuse image: " + (filename or image.name))
            converted[image.name] = None
            continue
        width, height = safe_image.size
        if width <= 0 or height <= 0 or width > 8192 or height > 8192 or width * height > 16000000:
            raise RuntimeError("FBX texture dimensions exceed the importer limit or cannot be decoded: " + image.name)
        output = target.parent / ("texture-" + str(len(converted) + 1) + ".png")
        safe_image.filepath_raw = str(output)
        safe_image.file_format = "PNG"
        safe_image.save()
        converted[image.name] = bpy.data.images.load(str(output), check_existing=False)
    for material in bpy.data.materials:
        if material.node_tree is not None:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image is not None:
                    node.image = converted.get(node.image.name)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    result = bpy.ops.wm.obj_export(filepath=str(target), check_existing=False,
        export_selected_objects=True, export_triangulated_mesh=True, export_uv=True,
        export_normals=True, export_materials=True, path_mode="STRIP",
        forward_axis="NEGATIVE_Z", up_axis="Y")
    if "FINISHED" not in result or not target.is_file():
        raise RuntimeError("Blender could not export the converted OBJ.")
    (target.parent / "conversion.json").write_text(json.dumps({"blenderVersion": bpy.app.version_string,
        "sourceVertices": vertices, "sourceFaces": triangles, "warnings": warnings}), encoding="utf-8")


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:]
    if len(argv) != 2:
        raise RuntimeError("Expected FBX input path and OBJ output path.")
    convert(Path(argv[0]), Path(argv[1]))
