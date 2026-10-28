using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class RestorePatch {
 static readonly string[] Allowed={"System.Void TitleMenu::Start()","System.Void TitleMenu::Update()","System.Void MMResult::Update()","System.Void GameManager::StartTitleMenu()"};
 static InvalidDataException Bad(string why)=>new InvalidDataException(why);
 static AssemblyDefinition Read(byte[] bytes)=>AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),new ReaderParameters{ReadSymbols=false});
 static void Parent(byte[] bytes){using var a=Read(bytes); if(a.MainModule.AssemblyReferences.Any(r=>r.Name=="RTDRestoreCache"))throw Bad("Already repaired parent");}
 static MethodDefinition Method(AssemblyDefinition a,string type,string name)=>a.MainModule.GetType(type)?.Methods.SingleOrDefault(m=>m.Name==name&&m.Parameters.Count==0)??throw Bad("Missing unique original method "+type+"."+name);
 static MethodReference Hook(AssemblyDefinition a,string name,int count) {
  if(a.Name.Name!="RTDRestoreCache")throw Bad("Unknown helper assembly name");
  var methods=a.MainModule.GetType("RTDRestoreCache")?.Methods.Where(m=>m.Name==name&&m.IsPublic&&m.IsStatic&&m.Parameters.Count==count).ToArray();
  if(methods==null||methods.Length!=1)throw Bad("Missing unique helper hook "+name);
  var m=methods[0];
  if(m.ReturnType.FullName!=(name=="BeforeResult"?"System.Boolean":"System.Void")
      ||count==4&&(m.Parameters[0].ParameterType.FullName!="UnityEngine.MonoBehaviour"||m.Parameters.Skip(1).Any(p=>p.ParameterType.FullName!="System.Int32"))
      ||count==1&&m.Parameters[0].ParameterType.FullName!="System.Boolean")throw Bad("Unexpected helper signature");
  return m;
 }
 static void Apply(AssemblyDefinition a,MethodReference reset,MethodReference mark,MethodReference gate,MethodReference exit) {
  var mod=a.MainModule;
  if(mod.AssemblyReferences.Any(r=>r.Name=="RTDRestoreCache"))throw Bad("Repair already referenced");
  var start=Method(a,"TitleMenu","Start");start.Body.GetILProcessor().InsertBefore(start.Body.Instructions[0],Instruction.Create(OpCodes.Call,mod.ImportReference(reset)));
  var leave=Method(a,"GameManager","StartTitleMenu");
  var won=mod.GetType("ClientData")?.Fields.SingleOrDefault(f=>f.Name=="m_BattleWin"&&f.IsStatic&&f.FieldType.FullName=="System.Boolean")??throw Bad("Missing original battle outcome field");
  var leaveFirst=leave.Body.Instructions[0];var leaveIl=leave.Body.GetILProcessor();
  leaveIl.InsertBefore(leaveFirst,Instruction.Create(OpCodes.Ldsfld,won));
  leaveIl.InsertBefore(leaveFirst,Instruction.Create(OpCodes.Call,mod.ImportReference(exit)));
  var title=Method(a,"TitleMenu","Update");
  var anchors=title.Body.Instructions.Where(i=>i.OpCode==OpCodes.Newobj&&i.Operand is MethodReference m&&m.DeclaringType.FullName=="MasterDataRTD.PartyInfo").ToArray();
  if(anchors.Length!=1||anchors[0].Next?.OpCode!=OpCodes.Stsfld||anchors[0].Next.Operand is not FieldReference target||target.FullName!="MasterDataRTD.PartyInfo ClientData::m_PartyInfo")throw Bad("Original restore PartyInfo construction shape changed");
  var anchor=anchors[0];var original=Instruction.Create(OpCodes.Newobj,(MethodReference)anchor.Operand);
  // Keep original branch targets on the marker, then execute the original newobj.
  anchor.OpCode=OpCodes.Call;anchor.Operand=mod.ImportReference(mark);title.Body.GetILProcessor().InsertAfter(anchor,original);
  var result=Method(a,"MMResult","Update");
  var mode=result.Body.Instructions.Select(i=>i.Operand).OfType<FieldReference>().FirstOrDefault(f=>f.Name=="m_Mode"&&f.FieldType.FullName=="System.Int32")??throw Bad("Missing original result mode");
  var current=result.DeclaringType.Fields.SingleOrDefault(f=>f.Name=="m_NowResultHierarchy"&&f.IsStatic&&f.FieldType.FullName=="System.Int32")??throw Bad("Missing original current hierarchy");
  var maximum=result.DeclaringType.Fields.SingleOrDefault(f=>f.Name=="m_MaxResultHierarchy"&&f.IsStatic&&f.FieldType.FullName=="System.Int32")??throw Bad("Missing original maximum hierarchy");
  var first=result.Body.Instructions[0];var il=result.Body.GetILProcessor();
  foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,mode),Instruction.Create(OpCodes.Ldsfld,current),Instruction.Create(OpCodes.Ldsfld,maximum),Instruction.Create(OpCodes.Call,mod.ImportReference(gate)),Instruction.Create(OpCodes.Brfalse,first),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,i);
 }
 public static byte[] Patch(byte[] parent,byte[] helper) {
  Parent(parent);using var a=Read(parent);using var h=Read(helper);
  Apply(a,Hook(h,"Reset",0),Hook(h,"MarkRestore",0),Hook(h,"BeforeResult",4),Hook(h,"BeforeBattleExit",1));
  using var stream=new MemoryStream();a.Write(stream);var bytes=stream.ToArray();Verify(parent,bytes);return bytes;
 }
 static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
 static string Body(MethodDefinition m) {
  if(!m.HasBody)return "no-body";
  string Operand(object value)=>value switch {
   null=>"",Instruction i=>"@"+m.Body.Instructions.IndexOf(i),Instruction[] list=>string.Join(",",list.Select(i=>"@"+m.Body.Instructions.IndexOf(i))),
   _=>value.ToString()};
  return string.Join("|",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(i.Operand)))+"#"+m.Body.InitLocals+":"+string.Join(",",m.Body.Variables.Select(v=>v.VariableType.FullName))+"#"+string.Join("|",m.Body.ExceptionHandlers.Select(h=>h.HandlerType+":"+Operand(h.TryStart)+":"+Operand(h.TryEnd)+":"+Operand(h.HandlerStart)+":"+Operand(h.HandlerEnd)+":"+Operand(h.FilterStart)+":"+h.CatchType?.FullName));
 }
 static string Layout(AssemblyDefinition a)=>string.Join("\n",Types(a.MainModule.Types).Select(t=>t.FullName+":"+t.Attributes+":"+t.BaseType?.FullName+":"+string.Join(",",t.Interfaces.Select(i=>i.InterfaceType.FullName))+"#"+string.Join("|",t.Fields.Select(f=>f.FullName+":"+f.Attributes+":"+f.Constant))+"#"+string.Join("|",t.Methods.Select(m=>m.FullName+":"+m.Attributes+":"+m.ImplAttributes))));
 public static string[] Verify(byte[] parent,byte[] candidate) {
  Parent(parent);using var before=Read(parent);using var after=Read(candidate);
  if(before.Name.FullName!=after.Name.FullName||before.MainModule.Mvid!=after.MainModule.Mvid||Layout(before)!=Layout(after))throw Bad("Unapproved managed layout/identity change");
  var old=Types(before.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
  var newer=Types(after.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
  var changed=old.Where(p=>Body(p.Value)!=Body(newer[p.Key])).Select(p=>p.Key).Order().ToArray();
  if(!changed.SequenceEqual(Allowed.Order()))throw Bad("Changes extend beyond the four allowed hooks");
  MethodReference Ref(string name)=>Types(after.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().FirstOrDefault(m=>m.DeclaringType.FullName=="RTDRestoreCache"&&m.Name==name)??throw Bad("Missing applied hook "+name);
  Apply(before,Ref("Reset"),Ref("MarkRestore"),Ref("BeforeResult"),Ref("BeforeBattleExit"));
  var expected=Types(before.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
  if(expected.Any(p=>Body(p.Value)!=Body(newer[p.Key])))throw Bad("Candidate differs from exact checked hook construction");
  var refs=after.MainModule.AssemblyReferences.Where(r=>r.Name!="RTDRestoreCache").Select(r=>r.FullName);
  var oldRefs=before.MainModule.AssemblyReferences.Where(r=>r.Name!="RTDRestoreCache").Select(r=>r.FullName);
  if(!refs.SequenceEqual(oldRefs))throw Bad("Unapproved assembly reference change: "+string.Join(";",refs)+" versus "+string.Join(";",oldRefs));
  return changed;
 }
}
