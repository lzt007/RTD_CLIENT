using System;using System.Reflection;using System.Collections.Generic;using System.Text;using UnityEngine;

int count=0;void Check(bool x,string why){if(!x)throw new Exception(why);count++;}

void Reset(){foreach(string name in new[]{"loaded","fontAttempted"})typeof(RTDLocalization).GetField(name,BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,false);typeof(RTDLocalization).GetField("replacement",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);typeof(RTDLocalization).GetField("map",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,new Dictionary<string,string>());typeof(RTDLocalization).GetField("formatted",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,new Dictionary<string,bool>());EmbeddedLocalization.Data.Clear();Resources.fonts=new UnityEngine.Object[0];}

void Data(string n,string s){EmbeddedLocalization.Data[n]=Encoding.UTF8.GetBytes(s);}

void Map(){Data("translation.json","{\"原文\":\"中文\",\"Hello {0}\":\"你好 {0}\"}");}

void Font(){EmbeddedLocalization.Data["font.png"]=new byte[]{1};string chars="中文你好{}0 X";string glyph="32\t32\t64\t64\n";foreach(char c in chars)glyph+=(int)c+"\t0\t0\t1\t1\t0\t0\t1\n";Data("font.tsv",glyph);Resources.fonts=new UnityEngine.Object[]{new UIFont()};}

Reset();Check(RTDLocalization.Display(new UILabel(),"原文")=="原文","missing map");

Reset();Data("translation.json","{\"原文\":\"中文\",oops}");Font();Check(RTDLocalization.Display(new UILabel(),"原文")=="原文","atomic corrupt map fallback");

Reset();EmbeddedLocalization.Data["translation.json"]=new byte[]{123,34,255,34,58,34,120,34,125};Font();Check(RTDLocalization.Display(new UILabel(),"原文")=="原文","invalid UTF8 fallback");

Reset();Map();Check(RTDLocalization.Display(new UILabel(),"原文")=="原文","missing font retains original");

Reset();Map();Font();var label=new UILabel();var original=label.font;Check(RTDLocalization.Display(label,"原文")=="中文"&&label.font!=original,"mapping and font injection");

Check(RTDLocalization.Display(label,"other")=="other"&&label.font==original,"reused label restores original font");
var untouched=new UILabel();var untouchedFont=untouched.font;Check(RTDLocalization.Display(untouched,"other")=="other"&&untouched.font==untouchedFont,"untranslated font unchanged");

var password=new UILabel{password=true};Check(RTDLocalization.Display(password,"原文")=="原文","password protected");var inputLabel=new UILabel();RTDLocalization.ProtectInput(new UIInput{label=inputLabel});Check(RTDLocalization.Display(inputLabel,"原文")=="原文","child label input protected");

Reset();Map();Font();string formatted=RTDLocalization.Format("Hello {0}",(object)"X");Check(formatted=="你好 X","template translated before first UILabel");var dynamicLabel=new UILabel();Check(RTDLocalization.Display(dynamicLabel,formatted)==formatted&&dynamicLabel.font!=null,"formatted font binding");Check(RTDLocalization.Format("Hello {0}",(object)"缺")=="Hello 缺","dynamic missing glyph returns original formatted template");

Reset();Map();Font();Data("translation.json","{\"Hello {0}\":\"你好 {9}\"}");Check(RTDLocalization.Format("Hello {0}",(object)"X")=="Hello X","corrupt format fallback");

Console.WriteLine("PASS "+count+" loader, font, input and template behavior checks (Unity test doubles)");

