namespace UnityEngine {
public class Object {}
public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 zero=>new(); }
public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} public static Vector2 zero=>new(); }
public struct Color { public float r,g,b,a; public Color(float x,float y,float z,float w){r=x;g=y;b=z;a=w;} public static Color white=>new(1,1,1,1); }
public class Mesh:Object { public string name=""; public Rendering.IndexFormat indexFormat; public Vector3[] vertices=[],normals=[]; public Vector2[] uv=[]; public int[] triangles=[]; public void RecalculateNormals(){} public void RecalculateBounds(){} }
public class Texture:Object {}
public class Texture2D:Texture { public int width=2,height=2; public Texture2D(int x,int y){} }
public static class ImageConversion { public static bool LoadImage(Texture2D t,byte[] b,bool m)=>true; }
public class Material:Object { public Material(Material m){} public bool HasProperty(string n)=>true; public void SetColor(string n,Color c){} public void SetTexture(string n,Texture? t){} }
public class Transform { public void SetParent(Transform t,bool b){} }
public class GameObject:Object { public GameObject(string n){} public Transform transform=new(); public void SetActive(bool b){} public T AddComponent<T>()where T:new()=>new(); }
public class MeshFilter { public Mesh? sharedMesh; }
public class MeshRenderer { public Material? sharedMaterial; }
}
namespace UnityEngine.Rendering { public enum IndexFormat{UInt32} }
