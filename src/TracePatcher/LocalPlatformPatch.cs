using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class LocalPlatformPatch {
 public static void Apply(AssemblyDefinition assembly) {
  var mod=assembly.MainModule;var shop=mod.GetType("MMShop");
  var open=mod.GetType("Dialog").Methods.Single(m=>m.Name=="Open1B_Direct"&&m.Parameters.Count==3);
  var mode=mod.GetType("UIBase").Fields.Single(f=>f.Name=="m_Mode");
  var dialog=shop.Fields.Single(f=>f.Name=="m_dialog");
  foreach(var name in new[]{"OnClickReview","OnClickNoahOffer"}) {
   var method=shop.Methods.Single(m=>m.Name==name);
   method.Body.Instructions.Clear();method.Body.Variables.Clear();method.Body.ExceptionHandlers.Clear();
   var il=method.Body.GetILProcessor();
   il.Emit(OpCodes.Ldarg_0);
   il.Emit(OpCodes.Ldstr,"LOCAL MODE\nStore reviews and advertising rewards are unavailable.\nNo currency was changed.");
   il.Emit(OpCodes.Ldstr,"BTN_OK");il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Call,open);
   il.Emit(OpCodes.Stfld,dialog);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4,303);
   il.Emit(OpCodes.Stfld,mode);il.Emit(OpCodes.Ret);
  }
  var gate=mod.GetType("MMQuest").Methods.Single(m=>m.Name=="SetEnableInductionButton");
  foreach(var instruction in gate.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldarg_1)) {
   instruction.OpCode=OpCodes.Ldc_I4_0;instruction.Operand=null;
  }
  Verify(assembly);
 }
 public static void Verify(AssemblyDefinition assembly) {
  var shop=assembly.MainModule.GetType("MMShop");
  foreach(var name in new[]{"OnClickReview","OnClickNoahOffer"}) {
   var method=shop.Methods.Single(m=>m.Name==name);
   var calls=method.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
   if(calls.Length!=1||calls[0].DeclaringType.Name!="Dialog"||calls[0].Name!="Open1B_Direct")
    throw new Exception(name+" must show only a local notice, without external requests");
   if(!method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&i.Operand is string s&&s.Contains("LOCAL MODE"))
      ||!method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4&&Convert.ToInt32(i.Operand)==303))
    throw new Exception(name+" must disclose local mode and use the existing dismiss path");
  }
  var gate=assembly.MainModule.GetType("MMQuest").Methods.Single(m=>m.Name=="SetEnableInductionButton");
  if(gate.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldarg_1))
   throw new Exception("External promotion button still depends on caller enable request");
  var active=gate.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="SetActiveRecursively");
  if(active.Previous.OpCode!=OpCodes.Ldc_I4_0)throw new Exception("External promotion button must be hidden");
  Console.WriteLine("Local platform contract verified: notices only; promotion hidden");
 }
}
