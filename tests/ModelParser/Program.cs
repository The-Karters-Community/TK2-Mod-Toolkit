using TK2.Customization;
using System.Globalization;
int checks=0; void Check(bool value,string why){ if(!value)throw new Exception(why); checks++; }
string folder=Path.Combine(Path.GetTempPath(),"tk2-model-tests-"+Guid.NewGuid());Directory.CreateDirectory(folder);
string path=Path.Combine(folder,"model.obj");
void Write(string content)=>File.WriteAllText(path,content);
void Reject(string content){Write(content); try{ObjModel.Parse(path,true);throw new Exception("Expected rejection");}catch(InvalidDataException){checks++;}}
try{
Write("v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nf -4/-4 -3/-3 -2/-2 -1/-1\n");
var normal=ObjModel.Parse(path,false); Check(normal.Parts.Count==1,"Part count"); Check(normal.Parts[0].Triangles.Count==6,"Quad fan"); Check(normal.Parts[0].Vertices[1].x==1,"Unmirrored X"); Check(normal.Parts[0].UV[1].x==1,"UV indexing"); Check(!normal.Parts[0].HasNormals,"Generated normals");
var mirrored=ObjModel.Parse(path,true);Check(mirrored.Parts[0].Vertices[1].x==-1&&mirrored.Parts[0].Vertices[1].y==1,"Mirror and reversed winding");
File.WriteAllText(Path.Combine(folder,"model.mtl"),"newmtl body\nKd 0.25 0.5 0.75\nmap_Kd color.png\n"); File.WriteAllBytes(Path.Combine(folder,"color.png"),[1]);
Write("mtllib model.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nusemtl body\nf 1//1 2//1 3//1\n");var material=ObjModel.Parse(path,true); Check(material.Surfaces["body"].Color.g==.5f,"Diffuse MTL");Check(material.Surfaces["body"].Texture==Path.Combine(folder,"color.png"),"Relative texture"); Check(material.Parts[0].HasNormals,"Imported normals");
Reject("v NaN 0 0\nf 1 1 1");Reject("v Infinity 0 0\nf 1 1 1");Reject("v 0 0 0\nf 1 2 3");Reject("v 0 0 0\nf 0 1 1");Reject("v 0 0 0\nf -2147483648 1 1");Reject("mtllib ../outside.mtl");Reject("# no faces");Reject("v 1000001 0 0\nf 1 1 1");
try{ModelPath.Resolve(folder,Path.Combine(folder,"model.obj"));throw new Exception("Absolute accepted");}catch(InvalidDataException){checks++;}
var previous=CultureInfo.CurrentCulture;CultureInfo.CurrentCulture=new CultureInfo("fi-FI"); Write("v 0.5 0 0\nv 0 1 0\nv 0 0 1\nf 1 2 3");Check(ObjModel.Parse(path,false).Parts[0].Vertices[0].x==.5f,"Invariant parsing");CultureInfo.CurrentCulture=previous;
byte[] png=new byte[24];png[0]=137;png[1]=80;png[2]=78;png[3]=71;png[18]=4;png[22]=2;Check(ObjModel.ImageSize(png)==(1024,512),"PNG dimensions");
byte[] jpeg=[255,216,255,192,0,7,8,2,0,4,0];Check(ObjModel.ImageSize(jpeg)==(1024,512),"JPEG dimensions");
png[16]=255;try{ObjModel.ImageSize(png);throw new Exception("Oversized accepted");}catch(InvalidDataException){checks++;}
try{ObjModel.ImageSize([1,2,3]);throw new Exception("Malformed accepted");}catch(InvalidDataException){checks++;}
Console.WriteLine($"Model parser: {checks} assertions passed (Unity stubs; parsing/path checks only).");
}finally{Directory.Delete(folder,true);}
