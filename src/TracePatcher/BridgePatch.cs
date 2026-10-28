using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
static class BridgePatch {
 public static void Apply(AssemblyDefinition fp,AssemblyDefinition unity){
 var m=fp.MainModule;var t=m.GetType("MySharedPreferencesCall");var jo=unity.MainModule.GetType("UnityEngine.AndroidJavaObject");var jc=unity.MainModule.GetType("UnityEngine.AndroidJavaClass");
 var jt=m.ImportReference(jo);var field=new FieldDefinition("rtdJavaPrefs",FieldAttributes.Private|FieldAttributes.Static,jt);t.Fields.Add(field);
 var get=new MethodDefinition("RTD_GetJavaPrefs",MethodAttributes.Private|MethodAttributes.Static,jt);t.Methods.Add(get);var il=get.Body.GetILProcessor();
 var done=Instruction.Create(OpCodes.Ldsfld,field);il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Brtrue,done);
 il.Emit(OpCodes.Ldstr,"jp.co.acquire.MySharedPreferences");il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);
 il.Emit(OpCodes.Ldstr,"com.unity3d.player.UnityPlayer");il.Emit(OpCodes.Newobj,m.ImportReference(jc.Methods.First(x=>x.IsConstructor&&x.Parameters.Count==1&&x.Parameters[0].ParameterType.FullName=="System.String")));
 il.Emit(OpCodes.Ldstr,"currentActivity");var g=new GenericInstanceMethod(m.ImportReference(jo.Methods.First(x=>x.Name=="GetStatic"&&x.HasGenericParameters)));g.GenericArguments.Add(jt);il.Emit(OpCodes.Callvirt,g);il.Emit(OpCodes.Stelem_Ref);
 il.Emit(OpCodes.Newobj,m.ImportReference(jo.Methods.First(x=>x.IsConstructor&&x.Parameters.Count==2&&x.Parameters[0].ParameterType.FullName=="System.String")));il.Emit(OpCodes.Stsfld,field);il.Append(done);il.Emit(OpCodes.Ret);
 foreach(var f in t.Methods.ToArray()){
 bool str=f.Name=="getString";if(!str&&!f.IsPInvokeImpl)continue;
 if(f.Name=="_getString"||f.Name=="_BufferRelease")continue;
 f.PInvokeInfo=null;f.IsPInvokeImpl=false;f.ImplAttributes=MethodImplAttributes.IL|MethodImplAttributes.Managed;f.Body=new MethodBody(f);il=f.Body.GetILProcessor();
 il.Emit(OpCodes.Call,get);il.Emit(OpCodes.Ldstr,str?"getString":f.Name.TrimStart('_'));
 il.Emit(OpCodes.Ldc_I4,f.Parameters.Count);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);
 for(int n=0;n<f.Parameters.Count;n++){il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4,n);il.Emit(OpCodes.Ldarg,f.Parameters[n]);if(f.Parameters[n].ParameterType.IsValueType)il.Emit(OpCodes.Box,f.Parameters[n].ParameterType);il.Emit(OpCodes.Stelem_Ref);}
 var ret=f.ReturnType;MethodReference call;
 if(ret.MetadataType==MetadataType.Void)call=m.ImportReference(jo.Methods.First(x=>x.Name=="Call"&&!x.HasGenericParameters&&x.Parameters.Count==2));
 else {var generic=new GenericInstanceMethod(m.ImportReference(jo.Methods.First(x=>x.Name=="Call"&&x.HasGenericParameters&&x.Parameters.Count==2)));generic.GenericArguments.Add(ret);call=generic;}
 il.Emit(OpCodes.Callvirt,call);il.Emit(OpCodes.Ret);
 if(str){
  // Old Unity's JNI signature resolver cannot represent a null string argument.
  // An absent key must still return the caller's original default, including null.
  var begin=f.Body.Instructions[0];
  var present=Instruction.Create(OpCodes.Ldarg_1);
  var normalized=Instruction.Create(OpCodes.Nop);
  foreach(var ins in new[]{Instruction.Create(OpCodes.Ldarg_0),
   Instruction.Create(OpCodes.Call,t.Methods.First(x=>x.Name=="Contains")),
   Instruction.Create(OpCodes.Brtrue,present),Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ret),
   present,Instruction.Create(OpCodes.Brtrue,normalized),Instruction.Create(OpCodes.Ldstr,""),
   Instruction.Create(OpCodes.Starg,f.Parameters[1]),normalized})il.InsertBefore(begin,ins);
 }
 }

 var au=m.GetType("AndroidUtilCall");var af=new FieldDefinition("rtdJavaFiles",FieldAttributes.Private|FieldAttributes.Static,jt);au.Fields.Add(af);
 var ag=new MethodDefinition("RTD_GetJavaFiles",MethodAttributes.Private|MethodAttributes.Static,jt);au.Methods.Add(ag);
 var map=get.Body.Instructions.ToDictionary(x=>x,x=>Instruction.Create(OpCodes.Nop));
 foreach(var old in get.Body.Instructions){var ni=map[old];ni.OpCode=old.OpCode;ni.Operand=old.Operand is Instruction target?map[target]:old.Operand==field?af:old.Operand is string name&&name=="jp.co.acquire.MySharedPreferences"?"jp.co.acquire.AndroidUtil":old.Operand;ag.Body.Instructions.Add(ni);}
 var load=au.Methods.First(x=>x.Name=="LoadFileDataBinary");var refs=load.Body.Instructions.Select(x=>x.Operand).OfType<MethodReference>().ToArray();var utf=refs.First(x=>x.Name=="get_UTF8");var decode=refs.First(x=>x.Name=="GetString");
 load.Body=new MethodBody(load){InitLocals=true};var arr=new ArrayType(m.TypeSystem.Byte);var bytes=new VariableDefinition(arr);load.Body.Variables.Add(bytes);il=load.Body.GetILProcessor();
 il.Emit(OpCodes.Call,ag);il.Emit(OpCodes.Ldstr,"LoadFileDataBinary");il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Stelem_Ref);
 var bc=new GenericInstanceMethod(m.ImportReference(jo.Methods.First(x=>x.Name=="Call"&&x.HasGenericParameters&&x.Parameters.Count==2)));bc.GenericArguments.Add(arr);il.Emit(OpCodes.Callvirt,bc);il.Emit(OpCodes.Stloc,bytes);il.Emit(OpCodes.Ldloc,bytes);var yes=Instruction.Create(OpCodes.Call,utf);il.Emit(OpCodes.Brtrue,yes);il.Emit(OpCodes.Ldnull);il.Emit(OpCodes.Ret);il.Append(yes);il.Emit(OpCodes.Ldloc,bytes);il.Emit(OpCodes.Callvirt,decode);il.Emit(OpCodes.Ret);
var fileType=new TypeReference("System.IO","File",m,m.TypeSystem.CoreLibrary);var exists=new MethodReference("Exists",m.TypeSystem.Boolean,fileType){HasThis=false};exists.Parameters.Add(new ParameterDefinition(m.TypeSystem.String));
var concat=new MethodReference("Concat",m.TypeSystem.String,m.TypeSystem.String){HasThis=false};concat.Parameters.Add(new ParameterDefinition(m.TypeSystem.String));concat.Parameters.Add(new ParameterDefinition(m.TypeSystem.String));
var first=load.Body.Instructions[0];foreach(var ins in new[]{Instruction.Create(OpCodes.Ldstr,"/data/data/jp.co.acquire.RTD/files/"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,concat),Instruction.Create(OpCodes.Call,exists),Instruction.Create(OpCodes.Brtrue,first),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,ins);

 // Preserve the APK Java implementation's UTF-8 bytes and private-file path,
 // while avoiding the legacy native JNI trampoline on translated ARM.
 var save=au.Methods.First(x=>x.Name=="SaveFileDataBinary");
 var encode=save.Body.Instructions.Select(x=>x.Operand).OfType<MethodReference>().First(x=>x.Name=="GetBytes");
 save.Body=new MethodBody(save);il=save.Body.GetILProcessor();
 il.Emit(OpCodes.Call,ag);il.Emit(OpCodes.Ldstr,"SaveFileDataBinary");il.Emit(OpCodes.Ldc_I4_2);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);
 il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Stelem_Ref);
 il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Call,utf);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Callvirt,encode);il.Emit(OpCodes.Stelem_Ref);
 il.Emit(OpCodes.Callvirt,m.ImportReference(jo.Methods.First(x=>x.Name=="Call"&&!x.HasGenericParameters&&x.Parameters.Count==2)));il.Emit(OpCodes.Ret);

 }
}

