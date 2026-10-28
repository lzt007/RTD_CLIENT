using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
public static class NoticePatch {
 public const string NoticeUrl="http://127.0.0.1:18765/rtd/local-notice";
 public const string TextUrl="http://127.0.0.1:18765/rtd/local/common-text4.json";
 const string OriginalTextUrl="https://rtd.acquire-spgmsv.jp/rtd/2.4.0/com_text4.json";
 static InvalidDataException Bad(string message)=>new InvalidDataException(message);
 static AssemblyDefinition Read(byte[] bytes)=>AssemblyDefinition.ReadAssembly(new MemoryStream(bytes));
 static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
 static string Body(MethodDefinition m){
  if(!m.HasBody)return "no-body";
  string Operand(object v)=>v switch{null=>"",Instruction i=>"@"+m.Body.Instructions.IndexOf(i),Instruction[] list=>string.Join(",",list.Select(i=>"@"+m.Body.Instructions.IndexOf(i))),_=>v.ToString()};
  return string.Join("|",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(i.Operand)))+"#"+m.Body.InitLocals+":"+string.Join(",",m.Body.Variables.Select(v=>v.VariableType.FullName))+"#"+string.Join("|",m.Body.ExceptionHandlers.Select(h=>h.HandlerType+":"+Operand(h.TryStart)+":"+Operand(h.TryEnd)+":"+Operand(h.HandlerStart)+":"+Operand(h.HandlerEnd)+":"+Operand(h.FilterStart)+":"+h.CatchType?.FullName));
 }
 static byte[] Construct(byte[] parent){
  // New maintenance chain validates exact IL structure below instead of historical output SHA.
  using var a=Read(parent);var resident=a.MainModule.GetType("ResidentManager")??throw Bad("Missing resident");
  var getter=resident.Methods.SingleOrDefault(m=>m.Name=="GetInfoAddress"&&m.Parameters.Count==0)??throw Bad("Missing getter");
  var il=getter.Body.Instructions;
  if(getter.ReturnType.FullName!="System.String" || il.Count!=4 || il[0].OpCode!=OpCodes.Ldarg_0 || il[1].OpCode!=OpCodes.Ldfld || il[2].OpCode!=OpCodes.Ldfld || il[3].OpCode!=OpCodes.Ret || getter.Body.ExceptionHandlers.Count!=0)throw Bad("Unexpected getter shape");
  var coroutine=resident.NestedTypes.SingleOrDefault(t=>t.Name=="<Coroutine_ComTextDownLoad>c__Iterator4B")?.Methods.SingleOrDefault(m=>m.Name=="MoveNext"&&m.Parameters.Count==0)??throw Bad("Missing original coroutine");
  var sites=coroutine.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,OriginalTextUrl)).ToArray();
  if(sites.Length!=2 || sites[0].Offset!=116 || sites[1].Offset!=127)throw Bad("Unexpected common-text download sites");
  il.Clear();getter.Body.Variables.Clear();getter.Body.InitLocals=false;il.Add(Instruction.Create(OpCodes.Ldstr,NoticeUrl));il.Add(Instruction.Create(OpCodes.Ret));
  foreach(var i in sites)i.Operand=TextUrl;
  using var output=new MemoryStream();a.Write(output,new WriterParameters{Timestamp=0});return output.ToArray();
 }
 public static byte[] Patch(byte[] parent){var candidate=Construct(parent);Verify(parent,candidate);return candidate;}
 public static string[] Verify(byte[] parent,byte[] candidate){
  var expected=Construct(parent);
  if(!expected.SequenceEqual(candidate))throw Bad("Candidate differs from exact deterministic notice construction");
  using var a=Read(parent);using var b=Read(candidate);
  var old=Types(a.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
  var newer=Types(b.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
  if(!old.Keys.Order().SequenceEqual(newer.Keys.Order()))throw Bad("Method layout changed");
  var changed=old.Where(p=>Body(p.Value)!=Body(newer[p.Key])).Select(p=>p.Key).Order().ToArray();
  var allowed=new[]{"System.String ResidentManager::GetInfoAddress()","System.Boolean ResidentManager/<Coroutine_ComTextDownLoad>c__Iterator4B::MoveNext()"};
  if(!changed.SequenceEqual(allowed.Order()))throw Bad("Unapproved method or exception-handler change");
  return changed;
 }
}
