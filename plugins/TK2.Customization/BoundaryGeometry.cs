using System;
using UnityEngine;

namespace TK2.Customization;

// Geometry comes from the collider itself. Native wrapper downcasts use TryCast,
// since a Collider wrapper need not be a managed MeshCollider instance.
internal sealed class BoundaryGeometry : IDisposable
{
    internal Mesh Mesh = null!;
    internal bool OwnsMesh;
    internal readonly Collider Source;
    private Vector3 _center, _size;
    private float _radius, _height, _capsuleRatio = -1;
    private int _shape, _direction;
    private BoundaryGeometry(Collider source) => Source = source;
    internal string ShapeName => _shape switch { 0 => "mesh", 1 => "box", 2 => "sphere", 3 => "capsule", _ => "unknown" };
    internal static BoundaryGeometry? Create(Collider source)
    {
        var result = new BoundaryGeometry(source);
        if (!result.Refresh()) return null;
        return result;
    }
    internal bool Refresh()
    {
        var mesh = Source.TryCast<MeshCollider>();
        if (mesh != null)
        {
            if (mesh.sharedMesh == null) return false;
            _shape = 0; Mesh = mesh.sharedMesh; return true;
        }
        var box = Source.TryCast<BoxCollider>();
        if (box != null)
        {
            _shape = 1; _center = box.center; _size = box.size;
            if (Mesh == null) { Mesh = Box(); OwnsMesh = true; }
            return true;
        }
        var sphere = Source.TryCast<SphereCollider>();
        if (sphere != null)
        {
            _shape = 2; _center = sphere.center; _radius = sphere.radius;
            if (Mesh == null) { Mesh = Round(2); OwnsMesh = true; }
            return true;
        }
        var capsule = Source.TryCast<CapsuleCollider>();
        if (capsule == null) return false;
        _shape = 3; _center = capsule.center; _radius = capsule.radius; _height = capsule.height; _direction = capsule.direction;
        UpdateCapsule(); return Mesh != null;
    }
    internal void Attach(Transform target)
    {
        // Mesh/box use the source's complete transform. Round colliders use uniform
        // world radii; inheriting non-uniform parent scale would draw an ellipsoid.
        if (_shape < 2) target.SetParent(Source.transform, false);
        Apply(target);
    }
    internal void Apply(Transform target)
    {
        if (_shape < 2)
        {
            target.localPosition = _shape == 0 ? Vector3.zero : _center;
            target.localRotation = Quaternion.identity;
            target.localScale = _shape == 0 ? Vector3.one : _size;
            return;
        }
        var scale = Source.transform.lossyScale;
        scale = new Vector3(Math.Abs(scale.x), Math.Abs(scale.y), Math.Abs(scale.z));
        float radial = _shape == 2 ? Math.Max(scale.x, Math.Max(scale.y, scale.z)) :
            _direction == 0 ? Math.Max(scale.y, scale.z) : _direction == 2 ? Math.Max(scale.x, scale.y) : Math.Max(scale.x, scale.z);
        target.position = Source.transform.TransformPoint(_center);
        target.rotation = _shape == 2 ? Quaternion.identity : Source.transform.rotation *
            (_direction == 0 ? Quaternion.Euler(0, 0, -90) : _direction == 2 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity);
        target.localScale = Vector3.one * (_radius * radial);
    }
    private void UpdateCapsule()
    {
        var scale = Source.transform.lossyScale;
        float axis = Math.Abs(_direction == 0 ? scale.x : _direction == 2 ? scale.z : scale.y);
        float radial = _direction == 0 ? Math.Max(Math.Abs(scale.y), Math.Abs(scale.z)) :
            _direction == 2 ? Math.Max(Math.Abs(scale.x), Math.Abs(scale.y)) : Math.Max(Math.Abs(scale.x), Math.Abs(scale.z));
        float radius = _radius * radial;
        float ratio = radius > 0 ? Math.Max(2, _height * axis / radius) : 2;
        if (Mesh != null && Math.Abs(ratio - _capsuleRatio) < .0001f) return;
        if (OwnsMesh && Mesh != null) UnityEngine.Object.Destroy(Mesh);
        Mesh = Round(ratio); OwnsMesh = true; _capsuleRatio = ratio;
    }
    private static Mesh Box()
    {
        var mesh = new Mesh { name = "TK2 boundary box", hideFlags = HideFlags.DontSave };
        mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
        mesh.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,1,2,6,1,6,5,3,0,4,3,4,7 };
        mesh.RecalculateBounds(); return mesh;
    }
    private static Mesh Round(float heightRatio)
    {
        const int sides = 24, halfRings = 8, rings = (halfRings + 1) * 2;
        var vertices = new Vector3[rings * (sides + 1)]; var triangles = new int[(rings - 1) * sides * 6];
        float offset = heightRatio * .5f - 1;
        for (int r = 0; r < rings; r++)
        {
            bool top = r > halfRings;
            double angle = top ? (r - halfRings - 1) * Math.PI / (2 * halfRings) : -Math.PI / 2 + r * Math.PI / (2 * halfRings);
            float y = (float)Math.Sin(angle) + (top ? offset : -offset), radius = (float)Math.Cos(angle);
            for (int s = 0; s <= sides; s++) { double a = s * 2 * Math.PI / sides; vertices[r * (sides + 1) + s] = new Vector3(radius * (float)Math.Cos(a), y, radius * (float)Math.Sin(a)); }
        }
        int i = 0;
        for (int r = 0; r < rings - 1; r++) for (int s = 0; s < sides; s++)
        { int a = r * (sides + 1) + s, b = a + sides + 1; triangles[i++] = a; triangles[i++] = b; triangles[i++] = a + 1; triangles[i++] = a + 1; triangles[i++] = b; triangles[i++] = b + 1; }
        var mesh = new Mesh { name = "TK2 boundary round volume", hideFlags = HideFlags.DontSave, vertices = vertices, triangles = triangles };
        mesh.RecalculateBounds(); return mesh;
    }
    public void Dispose() { if (OwnsMesh && Mesh != null) UnityEngine.Object.Destroy(Mesh); }
}
