using System;using System.Collections.Generic;
namespace UnityEngine {
 public class Object {public static void DontDestroyOnLoad(Object o){} }
 public class Component:Object {public GameObject gameObject=new GameObject();public object GetComponent(Type t){return gameObject.components.ContainsKey(t)?gameObject.components[t]:null;}}
 public class MonoBehaviour:Component {}
 public class GameObject:Object {public Dictionary<Type,object> components=new Dictionary<Type,object>();public GameObject(){}public GameObject(string n){}public object AddComponent(Type t){var c=Activator.CreateInstance(t);components[t]=c;return c;}}
 public static class Application {public static string persistentDataPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rtd-loader-tests-"+Guid.NewGuid());}
 public static class Debug {public static void Log(object o){} }
 public class Texture2D:Object {public int width=64,height=64;public Texture2D(int w,int h){}public bool LoadImage(byte[] b){return b.Length>0;}}
 public class Material:Object {public object mainTexture;public Material(Material m){}public Material(){} }
 public struct Rect {public Rect(float a,float b,float c,float d){} }
 public static class Resources {public static Object[] fonts=new Object[0];public static Object[] FindObjectsOfTypeAll(Type t){return fonts;} }
}
public class BMFont {public int charSize,baseOffset,texWidth,texHeight;public Dictionary<int,BMGlyph> glyphs=new Dictionary<int,BMGlyph>();public BMGlyph GetGlyph(int n){return GetGlyph(n,false);}public BMGlyph GetGlyph(int n,bool create){if(!glyphs.ContainsKey(n)&&create)glyphs[n]=new BMGlyph();return glyphs.ContainsKey(n)?glyphs[n]:null;}}
public class BMGlyph {public int x,y,width,height,offsetX,offsetY,advance;}
public class UIFont:UnityEngine.MonoBehaviour {public UnityEngine.Material material=new UnityEngine.Material();public BMFont bmFont=new BMFont();public UnityEngine.Rect uvRect;}
public class UILabel:UnityEngine.MonoBehaviour {public bool password;public UIFont font=new UIFont();}
public class UIInput:UnityEngine.MonoBehaviour {public UILabel label;}
public static class EmbeddedLocalization {public static Dictionary<string,byte[]> Data=new Dictionary<string,byte[]>();public static byte[] Read(string n){return Data.ContainsKey(n)?Data[n]:null;}}
