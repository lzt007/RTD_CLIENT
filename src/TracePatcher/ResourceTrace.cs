using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
static class ResourceTrace {
 public static void Apply(AssemblyDefinition asm,AssemblyDefinition unity,MethodReference log) {
  var mod=asm.MainModule;
  foreach(var name in new[]{"AssetBundleLoader","TitleMenu"}) {
   var t=mod.GetType(name);var method=t.Methods.First(m=>m.Name=="Update");var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];
   var count=new FieldDefinition("rtdResourceTraceCount",FieldAttributes.Private|FieldAttributes.Static,mod.TypeSystem.Int32);t.Fields.Add(count);
   void Add(Instruction x)=>il.InsertBefore(first,x);
   Add(Instruction.Create(OpCodes.Ldsfld,count));Add(Instruction.Create(OpCodes.Ldc_I4_1));Add(Instruction.Create(OpCodes.Add));Add(Instruction.Create(OpCodes.Dup));Add(Instruction.Create(OpCodes.Stsfld,count));Add(Instruction.Create(OpCodes.Ldc_I4,180));Add(Instruction.Create(OpCodes.Rem));Add(Instruction.Create(OpCodes.Brtrue,first));
   Add(Instruction.Create(OpCodes.Ldstr,"RTD_RESOURCE "+name));Add(Instruction.Create(OpCodes.Call,log));
   var fields=t.Fields.Where(f=>new[]{"m_Phase","m_LoadAssetID","m_FileURL","m_bLoadFinish"}.Contains(f.Name));
   if(name=="TitleMenu")fields=mod.GetType("UIBase").Fields.Where(f=>f.Name=="m_Mode"||f.Name=="m_Pause");
   foreach(var field in fields){Add(Instruction.Create(OpCodes.Ldstr,field.Name));Add(Instruction.Create(OpCodes.Call,log));Add(Instruction.Create(OpCodes.Ldarg_0));Add(Instruction.Create(OpCodes.Ldfld,field));if(field.FieldType.IsValueType)Add(Instruction.Create(OpCodes.Box,field.FieldType));Add(Instruction.Create(OpCodes.Call,log));}
   var ready=mod.ImportReference(unity.MainModule.GetType("UnityEngine.Caching").Methods.First(m=>m.Name=="get_ready"));Add(Instruction.Create(OpCodes.Ldstr,"Caching.ready"));Add(Instruction.Create(OpCodes.Call,log));Add(Instruction.Create(OpCodes.Call,ready));Add(Instruction.Create(OpCodes.Box,mod.TypeSystem.Boolean));Add(Instruction.Create(OpCodes.Call,log));
  }
 }
}
