using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class NetworkTracePatch {
 public static void Apply(AssemblyDefinition assembly, AssemblyDefinition flow, MethodReference log) {
  var mod=assembly.MainModule;
  var send=mod.ImportReference(flow.MainModule.GetType("RTDNetworkTrace").Methods.Single(m=>m.Name=="Send"));
  int count=0;
  foreach(var type in mod.GetTypes()) foreach(var method in type.Methods.Where(m=>m.HasBody)) {
   foreach(var instruction in method.Body.Instructions) {
    if(instruction.OpCode==OpCodes.Newobj && instruction.Operand is MethodReference ctor && ctor.DeclaringType.FullName=="UnityEngine.WWW" && ctor.Parameters.Count==3 && ctor.Parameters[2].ParameterType.FullName=="System.Collections.Hashtable") {
     instruction.OpCode=OpCodes.Call;instruction.Operand=send;count++;
    }
   }
  }
  var com=mod.GetType("ComManager");
  foreach(var method in com.Methods.Where(m=>m.HasBody && (m.Name.StartsWith("CreateRequest_") || new[]{"CreateUser","AuthorizeUser","FirstAuthorizeUser","SequenceCoroutine_Common","ResponseCoroutine_Authorize","ResponseCoroutine_Common","ResponseCoroutine_CreateUser","Coroutine_Download"}.Contains(m.Name)))) {
   var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];
   il.InsertBefore(first,Instruction.Create(OpCodes.Ldstr,"RTD_NET STAGE "+method.Name));
   il.InsertBefore(first,Instruction.Create(OpCodes.Call,log));
  }
  if(count<3)throw new Exception("Expected at least three legacy WWW POST sites; found "+count);
  Verify(assembly);
  Console.WriteLine("Network diagnostic WWW sites: "+count);
 }
 public static void Verify(AssemblyDefinition assembly) {
  var instructions=assembly.MainModule.GetTypes().SelectMany(t=>t.Methods.Where(m=>m.HasBody)).SelectMany(m=>m.Body.Instructions).ToArray();
  if(instructions.Any(i=>i.OpCode==OpCodes.Newobj && i.Operand is MethodReference m && m.DeclaringType.FullName=="UnityEngine.WWW" && m.Parameters.Count==3 && m.Parameters[2].ParameterType.FullName=="System.Collections.Hashtable"))throw new Exception("Untraced WWW POST constructor remains");
  int calls=instructions.Count(i=>i.OpCode==OpCodes.Call && i.Operand is MethodReference m && m.DeclaringType.FullName=="RTDNetworkTrace" && m.Name=="Send");
  if(calls!=5)throw new Exception("Expected five traced WWW POST sites; found "+calls);
  Console.WriteLine("Network trace contract verified: five POST sites, including coroutine bodies");
 }
}
