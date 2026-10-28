using System;using System.Linq;using Mono.Cecil;using Mono.Cecil.Cil;
int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
var d=TranslationMap.Parse("{\"源\\n{0}\":\"中文\\n{0}\",\"empty\":\" \"}");Check(d.Count==1&&d["源\n{0}"]=="中文\n{0}","exact/placeholder/newline/empty");
foreach(string bad in new[]{"[]","null","{\"a\":1}","{\"a\":\"b\",\"a\":\"b\"}","{\"a\":\"b\",}","{\"a\":\"\\x\"}","{}garbage","{\"a\":\"x\n\"}"}){bool failed=false;try{TranslationMap.Parse(bad);}catch{failed=true;}Check(failed,"reject malformed "+bad);}
Check(TranslationMap.Parse("{}").Count==0,"empty map");Check(TranslationMap.Parse("{\"\\u6e90\":\"\\u4e2d\"}")["源"]=="中","unicode escapes");
using var original=AssemblyDefinition.ReadAssembly(args[0]);using var final=AssemblyDefinition.ReadAssembly(args[1]);
string Body(MethodDefinition m)=>string.Join("|",m.Body.Instructions.Select(i=>i.OpCode+":"+i.Operand));
foreach(string name in new[]{"Get","GetText"})Check(Body(original.MainModule.GetType("GameText").Methods.Single(m=>m.Name==name))==Body(final.MainModule.GetType("GameText").Methods.Single(m=>m.Name==name)),"business GameText getter unchanged");
Check(Body(original.MainModule.GetType("UILabel").Methods.Single(m=>m.Name=="get_text"))==Body(final.MainModule.GetType("UILabel").Methods.Single(m=>m.Name=="get_text")),"UILabel business getter unchanged");
var process=final.MainModule.GetType("UILabel").Methods.Single(m=>m.Name=="ProcessText");Check(process.Body.Instructions.Count(i=>i.Operand is MethodReference mr&&mr.DeclaringType.Name=="RTDLocalization"&&mr.Name=="Display")==1,"display hook exactly once");
var hook=process.Body.Instructions.Single(i=>i.Operand is MethodReference mr&&mr.DeclaringType.Name=="RTDLocalization"&&mr.Name=="Display");Check(hook.Previous.OpCode==OpCodes.Ldfld&&((FieldReference)hook.Previous.Operand).Name=="mText"&&hook.Next.OpCode==OpCodes.Ldstr,"translate before Replace/layout");
Check(final.MainModule.GetType("UIInput").Methods.Single(m=>m.Name=="Init").Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="ProtectInput"),"input registration hook");
Check(final.MainModule.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference mr&&mr.Name=="Format"&&mr.DeclaringType.Name=="RTDLocalization"),"template hooks exist");
Console.WriteLine("PASS "+count+" parser and IL contract checks");
