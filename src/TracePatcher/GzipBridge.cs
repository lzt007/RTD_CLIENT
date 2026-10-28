using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class GzipBridge {
 public static void Apply(AssemblyDefinition fp, AssemblyDefinition unity) {
  var m=fp.MainModule;var t=m.GetType("GZipArchiveCall");
  var jo=unity.MainModule.GetType("UnityEngine.AndroidJavaObject");
  var jc=unity.MainModule.GetType("UnityEngine.AndroidJavaClass");
  var jt=m.ImportReference(jo);var arr=new ArrayType(m.TypeSystem.Byte);
  var field=new FieldDefinition("rtdJavaGzip",FieldAttributes.Private|FieldAttributes.Static,jt);t.Fields.Add(field);
  var get=new MethodDefinition("RTD_GetJavaGzip",MethodAttributes.Private|MethodAttributes.Static,jt);t.Methods.Add(get);
  var il=get.Body.GetILProcessor();var done=Instruction.Create(OpCodes.Ldsfld,field);
  il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Brtrue,done);
  il.Emit(OpCodes.Ldstr,"org.example.ScriptBridge.GZipArchive");
  il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);
  il.Emit(OpCodes.Ldstr,"com.unity3d.player.UnityPlayer");
  il.Emit(OpCodes.Newobj,m.ImportReference(jc.Methods.First(x=>x.IsConstructor&&x.Parameters.Count==1&&x.Parameters[0].ParameterType.FullName=="System.String")));
  il.Emit(OpCodes.Ldstr,"currentActivity");
  var g=new GenericInstanceMethod(m.ImportReference(jo.Methods.First(x=>x.Name=="GetStatic"&&x.HasGenericParameters)));g.GenericArguments.Add(jt);
  il.Emit(OpCodes.Callvirt,g);il.Emit(OpCodes.Stelem_Ref);
  il.Emit(OpCodes.Newobj,m.ImportReference(jo.Methods.First(x=>x.IsConstructor&&x.Parameters.Count==2&&x.Parameters[0].ParameterType.FullName=="System.String")));
  il.Emit(OpCodes.Stsfld,field);il.Append(done);il.Emit(OpCodes.Ret);
  var log=m.ImportReference(unity.MainModule.GetType("UnityEngine.Debug").Methods.First(x=>x.Name=="Log"&&x.Parameters.Count==1&&x.Parameters[0].ParameterType.FullName=="System.Object"));
  foreach(var name in new[]{"Compress","Decompress"}) {
   var method=t.Methods.First(x=>x.Name=="Call"+name);method.Body=new MethodBody(method);
   il=method.Body.GetILProcessor();
   il.Emit(OpCodes.Ldstr,"RTD_GZIP "+name);il.Emit(OpCodes.Call,log);
   il.Emit(OpCodes.Call,get);il.Emit(OpCodes.Ldstr,name);
   il.Emit(OpCodes.Ldc_I4_1);il.Emit(OpCodes.Newarr,m.TypeSystem.Object);il.Emit(OpCodes.Dup);il.Emit(OpCodes.Ldc_I4_0);il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Stelem_Ref);
   var call=new GenericInstanceMethod(m.ImportReference(jo.Methods.First(x=>x.Name=="Call"&&x.HasGenericParameters&&x.Parameters.Count==2)));call.GenericArguments.Add(arr);
   il.Emit(OpCodes.Callvirt,call);il.Emit(OpCodes.Ret);
  }
 }
}
