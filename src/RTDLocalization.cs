using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
public static class RTDLocalization {
 static bool loaded,fontAttempted;static Dictionary<string,bool> formatted=new Dictionary<string,bool>();static Dictionary<string,string> map=new Dictionary<string,string>();static UIFont replacement;
 public static void ProtectInput(UIInput input){if(input!=null&&input.label!=null)if(input.label.GetComponent(typeof(RTDInputProtected))==null)input.label.gameObject.AddComponent(typeof(RTDInputProtected));}
 static byte[] Read(string name){string path=Application.persistentDataPath+"/rtd-localization/"+name;if(File.Exists(path))return File.ReadAllBytes(path);return EmbeddedLocalization.Read(name);}

 static string Text(byte[] data){return data==null?null:new System.Text.UTF8Encoding(false,true).GetString(data);}
 static void Load(){if(loaded)return;loaded=true;try {byte[] raw=Read("translation.json");if(raw!=null)map=TranslationMap.Parse(Text(raw));}catch(Exception){map.Clear();Debug.Log("RTD localization: missing/invalid mapping; original text retained");}}
 static bool Font(UIFont original){if(replacement!=null)return true;if(original==null)return false;if(fontAttempted)return false;fontAttempted=true;try {
  byte[] png=Read("font.png");string data=Text(Read("font.tsv"));if(png==null||data==null||original==null){Debug.Log("RTD localization: optional font absent; original text retained");return false;}
  var texture=new Texture2D(2,2);if(!texture.LoadImage(png))return false;
  string[] lines=data.Replace("\r","").Split('\n');string[] head=lines[0].Split('\t');if(head.Length!=4)throw new FormatException();
  var obj=new GameObject("RTD localized bitmap font");UnityEngine.Object.DontDestroyOnLoad(obj);var font=(UIFont)obj.AddComponent(typeof(UIFont));
  font.bmFont.charSize=int.Parse(head[0]);font.bmFont.baseOffset=int.Parse(head[1]);font.bmFont.texWidth=int.Parse(head[2]);font.bmFont.texHeight=int.Parse(head[3]);
  if(texture.width!=font.bmFont.texWidth||texture.height!=font.bmFont.texHeight)throw new FormatException();
  for(int i=1;i<lines.Length;i++){if(lines[i].Length==0)continue;string[] v=lines[i].Split('\t');if(v.Length!=8)throw new FormatException();BMGlyph g=font.bmFont.GetGlyph(int.Parse(v[0]),true);g.x=int.Parse(v[1]);g.y=int.Parse(v[2]);g.width=int.Parse(v[3]);g.height=int.Parse(v[4]);g.offsetX=int.Parse(v[5]);g.offsetY=int.Parse(v[6]);g.advance=int.Parse(v[7]);}
  var material=new Material(original.material);material.mainTexture=texture;font.material=material;font.uvRect=new Rect(0,0,1,1);replacement=font;return true;
 }catch(Exception){Debug.Log("RTD localization: invalid optional font; original text retained");return false;}}
 static bool Covers(string text){foreach(char c in text)if(!char.IsWhiteSpace(c)&&replacement.bmFont.GetGlyph((int)c)==null)return false;return true;}
 static bool EnsureFont(){if(replacement!=null)return true;if(fontAttempted)return false;UnityEngine.Object[] fonts=Resources.FindObjectsOfTypeAll(typeof(UIFont));foreach(UnityEngine.Object obj in fonts){UIFont font=obj as UIFont;if(font!=null&&font.material!=null)return Font(font);}return false;}
 static string Render(string source,object[] args){Load();string value;if(source!=null&&map.TryGetValue(source,out value)&&EnsureFont()){try{string result=string.Format(value,args);if(Covers(result)){if(formatted.Count>=256)formatted.Clear();formatted[result]=true;return result;}}catch(FormatException){}}return string.Format(source,args);}
 public static string Format(string source,object arg0){return Render(source,new object[]{arg0});}
 public static string Format(string source,object arg0,object arg1){return Render(source,new object[]{arg0,arg1});}
 public static string Format(string source,object arg0,object arg1,object arg2){return Render(source,new object[]{arg0,arg1,arg2});}
 public static string Format(string source,object[] args){return Render(source,args);}
 public static string Template(string source){Load();string value;if(replacement!=null&&source!=null&&map.TryGetValue(source,out value)&&Covers(value))return value;return source;}
 static string Original(UILabel label,string source){if(label!=null){RTDLocalizedLabel state=label.GetComponent(typeof(RTDLocalizedLabel)) as RTDLocalizedLabel;if(state!=null&&label.font==replacement)label.font=state.original;}return source;}
 public static string Display(UILabel label,string source){Load();if(label==null||source==null||label.password||label.GetComponent(typeof(RTDInputProtected))!=null||label.GetComponent(typeof(UIInput))!=null)return Original(label,source);string value;if(map.Count==0||!Font(label.font))return Original(label,source);if(!map.TryGetValue(source,out value)){if(!formatted.ContainsKey(source))return Original(label,source);value=source;}if(!Covers(value))return Original(label,source);RTDLocalizedLabel state=label.GetComponent(typeof(RTDLocalizedLabel)) as RTDLocalizedLabel;if(state==null)state=(RTDLocalizedLabel)label.gameObject.AddComponent(typeof(RTDLocalizedLabel));if(label.font!=replacement)state.original=label.font;label.font=replacement;return value;}

}

namespace System.Runtime.CompilerServices {
 [AttributeUsage(AttributeTargets.Assembly)] public sealed class CompilationRelaxationsAttribute:Attribute {public CompilationRelaxationsAttribute(int value){} }
 [AttributeUsage(AttributeTargets.Assembly)] public sealed class RuntimeCompatibilityAttribute:Attribute {public bool WrapNonExceptionThrows {get;set;} }
}

public class RTDInputProtected:MonoBehaviour {}

public class RTDLocalizedLabel:MonoBehaviour {public UIFont original;}
