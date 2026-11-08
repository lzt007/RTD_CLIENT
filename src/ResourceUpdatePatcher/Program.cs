using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
class Program {
 static IEnumerable<TypeDefinition> Types(TypeDefinition type){yield return type;foreach(var nested in type.NestedTypes)foreach(var item in Types(nested))yield return item;}
 static IEnumerable<MethodDefinition> Methods(ModuleDefinition module){return module.Types.SelectMany(Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody);}
 static MethodDefinition Method(ModuleDefinition module,string type,string name){return module.GetType(type).Methods.Single(m=>m.Name==name);}
 static void Require(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
 static void Main(string[] args){
  if(args.Length==2&&args[0]=="verify"){using(var asm=AssemblyDefinition.ReadAssembly(args[1]))Verify(asm.MainModule);Console.WriteLine("Resource gate verification passed");return;}
  if(args.Length==3&&args[0]=="native-io"){
   using(var asm=AssemblyDefinition.ReadAssembly(args[1])){var module=asm.MainModule;var library=new ModuleReference("libc.so");module.ModuleReferences.Add(library);
    foreach(string name in new[]{"Open","Sync","Close"}){var method=Method(module,"RTD.ResourceUpdates.ResourceFiles","Native"+name);Require(!method.IsPInvokeImpl,"Native IO already patched");method.Body=null;method.Attributes|=MethodAttributes.PInvokeImpl;method.ImplAttributes=MethodImplAttributes.PreserveSig;method.PInvokeInfo=new PInvokeInfo(PInvokeAttributes.CallConvCdecl,name=="Open"?"open":name=="Sync"?"fsync":"close",library);}
    asm.Write(args[2]);}return;
  }
  Require(args.Length==5&&args[0]=="apply","apply MAIN CORE BRIDGE OUTPUT | verify MAIN | native-io CORE OUTPUT");
  Require(!File.Exists(args[4]),"Output already exists");
  using(var asm=AssemblyDefinition.ReadAssembly(args[1]))using(var core=AssemblyDefinition.ReadAssembly(args[2]))using(var bridge=AssemblyDefinition.ReadAssembly(args[3])){
   Require(Methods(bridge.MainModule).Where(m=>m.DeclaringType.Name.Contains("WrapLevel")).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is FieldReference&&((FieldReference)i.Operand).Name=="CheckedResources"),"Level lease must cover the resolved dependency closure");
   var se=bridge.MainModule.GetType("RTDResourceGameBridge").NestedTypes.Single(t=>t.Name=="GameHooks").Methods.Single(m=>m.Name=="RefreshAudio");
   var seCalls=se.Body.Instructions.Where(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).Name=="AudioName").ToArray();
   Require(seCalls.Length==2&&seCalls[1].Previous.OpCode==OpCodes.Add&&seCalls[1].Previous.Previous.OpCode==OpCodes.Ldc_I4_1,"SE slot mapping must match original one-based resource index");
   var module=asm.MainModule;var loader=module.GetType("AssetBundleLoader");Require(loader!=null&&!loader.Methods.Any(m=>m.Name=="RTDDownloadStartOriginal"),"Unknown or already patched game");
   var download=Method(module,"AssetBundleLoader","DownloadStart");Require(download.Parameters.Count==0&&download.Body.Instructions.Count(i=>i.OpCode==OpCodes.Stfld)==4,"Unknown startup shape");
   var level=Method(module,"LevelLoader","InitMap");var single=Method(module,"AssetBundleLoader","LoadAssetBundle_Single");Require(level.ReturnType.FullName=="System.Collections.IEnumerator"&&single.Parameters.Count==1,"Unknown iterator gates");
   Func<string,MethodReference> hook=name=>module.ImportReference(Method(bridge.MainModule,"RTDResourceGameBridge",name));
   int loads=0,caches=0,audio=0;
   foreach(var method in Methods(module).ToArray())foreach(var instruction in method.Body.Instructions){var called=instruction.Operand as MethodReference;if(called==null)continue;
    if(called.Name=="LoadFromCacheOrDownload"&&(called.DeclaringType.FullName=="UnityEngine.WWW"||called.DeclaringType.FullName=="RTDEmbeddedCache")){instruction.OpCode=OpCodes.Call;instruction.Operand=module.ImportReference(Method(core.MainModule,"RTD.ResourceUpdates.ResourceRuntime",called.Name));loads++;}
    if(called.Name=="IsVersionCached"&&(called.DeclaringType.FullName=="UnityEngine.Caching"||called.DeclaringType.FullName=="RTDEmbeddedCache")){instruction.OpCode=OpCodes.Call;instruction.Operand=module.ImportReference(Method(core.MainModule,"RTD.ResourceUpdates.ResourceRuntime",called.Name));caches++;}
    if(method.DeclaringType.FullName=="SoundManager"&&called.DeclaringType.FullName=="UnityEngine.AudioSource"&&called.Name=="set_clip"){instruction.OpCode=OpCodes.Call;instruction.Operand=hook("SetAudioClip");audio++;}
   }
   Require(loads==3&&caches>=3&&audio>=3,"Unknown resource call coverage");
   Wrap(level,hook("WrapLevel"),false);Wrap(single,hook("WrapSingle"),true);
   var clone=new MethodDefinition("RTDDownloadStartOriginal",MethodAttributes.Public|MethodAttributes.HideBySig,download.ReturnType);loader.Methods.Add(clone);clone.Body=download.Body;
   download.Body=new MethodBody(download);var il=download.Body.GetILProcessor();il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Ldc_I4_0));il.Append(Instruction.Create(OpCodes.Stfld,loader.Fields.Single(f=>f.Name=="m_bLoadFinish")));
   var enabled=Methods(module).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand as MethodReference).First(m=>m!=null&&m.DeclaringType.FullName=="UnityEngine.Behaviour"&&m.Name=="set_enabled");
   il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Ldc_I4_0));il.Append(Instruction.Create(OpCodes.Call,module.ImportReference(enabled)));
   il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Call,hook("StartStartup")));il.Append(Instruction.Create(OpCodes.Ret));
   Verify(module);asm.Write(args[4]);
  }
  using(var check=AssemblyDefinition.ReadAssembly(args[4]))Verify(check.MainModule);
  Console.WriteLine("Patched and reread resource startup/level/single gates and three remote downloads");
 }
 static void Wrap(MethodDefinition method,MethodReference hook,bool index){
  var returns=method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray();Require(returns.Length==1&&method.Body.ExceptionHandlers.Count==0,"Unknown iterator factory body");
  // Mutate the existing ret, preserving all branch targets to it.
  var ret=returns[0];ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;var il=method.Body.GetILProcessor();var after=ret;
  if(index){var arg=Instruction.Create(OpCodes.Ldarg_1);il.InsertAfter(after,arg);after=arg;}
  var call=Instruction.Create(OpCodes.Call,hook);il.InsertAfter(after,call);il.InsertAfter(call,Instruction.Create(OpCodes.Ret));method.Body.MaxStackSize+=3;
 }
 static void Verify(ModuleDefinition module){
  Require(module.GetType("AssetBundleLoader")!=null,"Missing loader");
  Require(Method(module,"AssetBundleLoader","RTDDownloadStartOriginal").HasBody,"Missing startup original");
  var calls=Methods(module).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand as MethodReference).Where(m=>m!=null).ToArray();
  Require(calls.Count(m=>m.DeclaringType.FullName=="RTD.ResourceUpdates.ResourceRuntime"&&m.Name=="LoadFromCacheOrDownload")==3,"Missing checked remote download");
  Require(!calls.Any(m=>(m.DeclaringType.FullName=="UnityEngine.WWW"||m.DeclaringType.FullName=="RTDEmbeddedCache")&&m.Name=="LoadFromCacheOrDownload"),"Unchecked remote download");
  foreach(var pair in new[]{new[]{"LevelLoader","InitMap","WrapLevel"},new[]{"AssetBundleLoader","LoadAssetBundle_Single","WrapSingle"},new[]{"AssetBundleLoader","DownloadStart","StartStartup"}}){var method=Method(module,pair[0],pair[1]);Require(method.Body.Instructions.Count(i=>i.Operand is MethodReference&&((MethodReference)i.Operand).DeclaringType.FullName=="RTDResourceGameBridge"&&((MethodReference)i.Operand).Name==pair[2])==1,"Gate missing or duplicated");}
 }
}
