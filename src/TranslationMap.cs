using System;
using System.Collections.Generic;
using System.Text;
public static class TranslationMap {
 public static Dictionary<string,string> Parse(string s) {
  var result=new Dictionary<string,string>(); int p=0;
  Skip(s,ref p); Expect(s,ref p,'{'); Skip(s,ref p);
  if(p<s.Length && s[p]=='}'){p++;Skip(s,ref p);if(p!=s.Length)throw new FormatException();return result;}
  while(true){string key=Read(s,ref p);Skip(s,ref p);Expect(s,ref p,':');string value=Read(s,ref p);if(result.ContainsKey(key))throw new FormatException("Duplicate key");result.Add(key,value);Skip(s,ref p);if(p<s.Length&&s[p]=='}'){p++;break;}Expect(s,ref p,',');}
  Skip(s,ref p);if(p!=s.Length)throw new FormatException();
  var clean=new Dictionary<string,string>();foreach(var pair in result)if(pair.Value.Trim().Length>0)clean.Add(pair.Key,pair.Value);return clean;
 }
 static void Skip(string s,ref int p){while(p<s.Length&&char.IsWhiteSpace(s[p]))p++;}
 static void Expect(string s,ref int p,char c){Skip(s,ref p);if(p>=s.Length||s[p++]!=c)throw new FormatException();}
 static string Read(string s,ref int p){Expect(s,ref p,'"');var b=new StringBuilder();while(p<s.Length){char c=s[p++];if(c=='"')return b.ToString();if(c<32)throw new FormatException();if(c=='\\'){if(p>=s.Length)throw new FormatException();c=s[p++];switch(c){case '"':case '\\':case '/':b.Append(c);break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'u':if(p+4>s.Length)throw new FormatException();b.Append((char)Convert.ToInt32(s.Substring(p,4),16));p+=4;break;default:throw new FormatException();}}else b.Append(c);}throw new FormatException();}
}
