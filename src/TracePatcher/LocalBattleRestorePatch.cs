using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class LocalBattleRestorePatch {
 static MethodDefinition Update(AssemblyDefinition assembly)=>assembly.MainModule.GetType("TitleMenu").Methods.Single(m=>m.Name=="Update");
 static bool IsOldPlayerAssignment(Instruction i)=>i.OpCode==OpCodes.Stsfld
  && i.Operand is FieldReference target && target.DeclaringType.Name=="ClientData" && target.Name=="m_ComPlayerData"
  && i.Previous?.OpCode==OpCodes.Ldfld && i.Previous.Operand is FieldReference source
  && source.DeclaringType.Name=="SaveGameRestore" && source.Name=="m_ComPlayerData";
 public static void Apply(AssemblyDefinition assembly) {
  var assignment=Update(assembly).Body.Instructions.Single(IsOldPlayerAssignment);
  var load=assignment.Previous.Previous;
  if(load.OpCode!=OpCodes.Ldsfld || load.Operand is not FieldReference field
     ||field.DeclaringType.Name!="SaveDataManager"||field.Name!="m_GameRestore")
   throw new Exception("Unexpected original battle player restore instruction shape");
  foreach(var instruction in new[]{load,assignment.Previous,assignment}) {
   instruction.OpCode=OpCodes.Nop;instruction.Operand=null;
  }
  Verify(assembly);
 }
 public static void Verify(AssemblyDefinition assembly) {
  var method=Update(assembly);
  if(method.Body.Instructions.Any(IsOldPlayerAssignment))
   throw new Exception("Battle restore still replaces authenticated player with stale local snapshot");
  if(!method.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="AuthorizeUser"))
   throw new Exception("Battle restore must retain original player authorization");
  Console.WriteLine("Local battle restore contract verified: authenticated player retained");
 }
}
