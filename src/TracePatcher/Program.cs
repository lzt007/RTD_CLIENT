using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
if(args.Length==2&&args[0]=="verify-local-store") {
 using var candidate=AssemblyDefinition.ReadAssembly(args[1]);LocalStorePatch.Verify(candidate);return;
}
if(args.Length==4&&args[0]=="patch-local-store") {
 var localResolver=new DefaultAssemblyResolver();localResolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(args[1]))!);
 using var candidate=AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters{AssemblyResolver=localResolver});using var flowAssembly=AssemblyDefinition.ReadAssembly(args[2]);
 LocalStorePatch.Apply(candidate,flowAssembly);candidate.Write(args[3]);return;
}
if(args.Length==3&&args[0]=="inspect-type-il") {
 using var a=AssemblyDefinition.ReadAssembly(args[1]);
 foreach(var t in a.MainModule.GetTypes().Where(t=>t.FullName==args[2]||t.FullName.StartsWith(args[2]+"/"))) {
  Console.WriteLine("TYPE "+t.FullName);foreach(var f in t.Fields)Console.WriteLine("FIELD "+f.FullName);
  foreach(var m in t.Methods) {Console.WriteLine("METHOD "+m.FullName);if(m.HasBody)foreach(var ins in m.Body.Instructions)Console.WriteLine(ins);}
 }return;
}
if(args.Length==3&&args[0]=="embed-cache-hook") {
 using var a=AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters{InMemory=true});using var helper=AssemblyDefinition.ReadAssembly(args[2]);
 var ensure=a.MainModule.ImportReference(helper.MainModule.GetType("RTDEmbeddedCache").Methods.Single(m=>m.Name=="Ensure"));int count=0;
 foreach(var t in a.MainModule.GetTypes())foreach(var m in t.Methods.Where(m=>m.HasBody)) {
  if((t.Name=="TitleMenu"||t.Name=="Installer"||t.Name=="AssetBundleLoader")&&m.Name=="Awake") {m.Body.GetILProcessor().InsertBefore(m.Body.Instructions[0],Instruction.Create(OpCodes.Call,ensure));count++;}
 }
 foreach(var t in a.MainModule.GetTypes())foreach(var m in t.Methods.Where(m=>m.HasBody))foreach(var ins in m.Body.Instructions) {
  if(ins.OpCode!=OpCodes.Call||ins.Operand is not MethodReference target)continue;
  if((target.DeclaringType.FullName=="UnityEngine.Caching"&&target.Name=="IsVersionCached")||
     (target.DeclaringType.FullName=="UnityEngine.WWW"&&target.Name=="LoadFromCacheOrDownload"&&target.Parameters.Count==2))
   ins.Operand=a.MainModule.ImportReference(helper.MainModule.GetType("RTDEmbeddedCache").Methods.Single(x=>x.Name==target.Name));
 }
 if(count<1)throw new Exception("Missing resource bootstrap entry");a.Write(args[1]);Console.WriteLine("Embedded cache startup hooks: "+count);return;
}
if(args.Length==3&&args[0]=="filter-logs") {
 LogLevelPatch.Apply(args[1],args[2]);return;
}
if(args.Length==2&&args[0]=="verify-network-trace") {
 using var candidate=AssemblyDefinition.ReadAssembly(args[1]);
 NetworkTracePatch.Verify(candidate);return;
}
if(args.Length==2&&args[0]=="verify-local-platform") {
 using var candidate=AssemblyDefinition.ReadAssembly(args[1]);
 LocalPlatformPatch.Verify(candidate);return;
}
if(args.Length==2&&args[0]=="verify-local-battle-restore") {
 using var candidate=AssemblyDefinition.ReadAssembly(args[1]);
 LocalBattleRestorePatch.Verify(candidate);return;
}
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(args[0]);
var asm=AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(args[0],"Assembly-CSharp.dll"),new ReaderParameters{AssemblyResolver=resolver});
var unity=AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(args[0],"UnityEngine.dll"));
var mod=asm.MainModule;
var log=mod.ImportReference(unity.MainModule.GetType("UnityEngine.Debug").Methods.First(m=>m.Name=="Log"&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.FullName=="System.Object"));

bool resource=args.Length>3&&args[3]=="resource";
bool quiet=args.Length>3&&(args[3]=="quiet"||resource);
foreach(var t in mod.Types){
 if(resource&&t.Name=="DebugLogger")foreach(var m in t.Methods.Where(m=>m.HasBody&&m.Parameters.Count==1)){m.Body.Instructions.Clear();var il=m.Body.GetILProcessor();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,log);il.Emit(OpCodes.Ret);}
 if(quiet)continue;
 if(t.Name=="DebugLogger")foreach(var m in t.Methods.Where(m=>m.HasBody&&m.Parameters.Count==1)){m.Body.Instructions.Clear();var il=m.Body.GetILProcessor();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,log);il.Emit(OpCodes.Ret);}
 
 foreach(var m in t.Methods.Where(m=>m.HasBody&&new[]{"Awake","Start","Update","BaseUpdate","LoadResident","Init","SetDontDestroyOnLoad","SetUserIDLabel"}.Contains(m.Name))){
 if(!new[]{"TitleMenu","SceneManager","ResidentManager","UIBase"}.Contains(t.Name) && m.Name!="Awake" && m.Name!="Start")continue;
 var il=m.Body.GetILProcessor();var first=m.Body.Instructions[0];
 foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){il.InsertBefore(ret,Instruction.Create(OpCodes.Ldstr,"RTD_EXIT "+t.Name+"."+m.Name));il.InsertBefore(ret,Instruction.Create(OpCodes.Call,log));}
 void Add(Instruction i)=>il.InsertBefore(first,i);
 bool periodic=m.Name=="Update"||m.Name=="BaseUpdate";
 
 Add(Instruction.Create(OpCodes.Ldstr,"RTD_TRACE "+t.Name+"."+m.Name));Add(Instruction.Create(OpCodes.Call,log));
 foreach(var field in t.Fields.Where(f=>!f.IsStatic&&new[]{"m_Mode","m_Seq"}.Contains(f.Name))){Add(Instruction.Create(OpCodes.Ldarg_0));Add(Instruction.Create(OpCodes.Ldfld,field));Add(Instruction.Create(OpCodes.Box,mod.TypeSystem.Int32));Add(Instruction.Create(OpCodes.Call,log));}
 if(t.Name=="UIBase") {var f=mod.GetType("SceneManager").Fields.First(f=>f.Name=="isLoaded");Add(Instruction.Create(OpCodes.Ldsfld,f));Add(Instruction.Create(OpCodes.Box,mod.TypeSystem.Boolean));Add(Instruction.Create(OpCodes.Call,log));}
 }
}
if(args.Length>2 && args[2]=="no-social") {
 var awake=mod.GetType("SocialManager").Methods.First(m=>m.Name=="Awake");var il=awake.Body.GetILProcessor();
 foreach(var ins in awake.Body.Instructions.ToArray())if(ins.Operand is MethodReference mr && mr.Name=="init" && (mr.DeclaringType.Name=="FacebookAndroid"||mr.DeclaringType.Name=="TwitterAndroid")){
 for(int i=0;i<mr.Parameters.Count;i++)il.InsertBefore(ins,Instruction.Create(OpCodes.Pop));
 ins.OpCode=OpCodes.Nop;ins.Operand=null;
 }
}
foreach(var t in mod.Types.Where(t=>!quiet&&new[]{"SaveDataManager","MySharedPreferencesCall","MySharedPreferences"}.Contains(t.Name)))foreach(var m in t.Methods.Where(m=>m.HasBody)) {
 var il=m.Body.GetILProcessor();
 foreach(var call in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Call||i.OpCode==OpCodes.Callvirt).ToArray()) {
 var name="RTD_CALL "+t.Name+"."+m.Name+" -> "+call.Operand;
 il.InsertBefore(call,Instruction.Create(OpCodes.Ldstr,name));il.InsertBefore(call,Instruction.Create(OpCodes.Call,log));
 }
}
var fp=AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(args[0],"Assembly-CSharp-firstpass.dll"),new ReaderParameters{AssemblyResolver=resolver});
var fl=fp.MainModule.ImportReference(log);
foreach(var t in fp.MainModule.Types.Where(t=>!quiet&&t.Name=="MySharedPreferencesCall"))foreach(var m in t.Methods.Where(m=>m.HasBody)) {
 var il=m.Body.GetILProcessor();foreach(var call in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Call||i.OpCode==OpCodes.Callvirt).ToArray()) {
 il.InsertBefore(call,Instruction.Create(OpCodes.Ldstr,"RTD_PREFS "+m.Name+" -> "+call.Operand));il.InsertBefore(call,Instruction.Create(OpCodes.Call,fl));
 }
}
BridgePatch.Apply(fp,unity);
if(resource) ResourceTrace.Apply(asm,unity,log);
if(args.Length>4&&args[4]=="offline") {
 var info=mod.GetType("AssetBundleDefine").NestedTypes.First(t=>t.Name=="AssetBundleInfo");
 foreach(var ins in info.Methods.First(m=>m.Name=="GetURL").Body.Instructions)
  if(ins.OpCode==OpCodes.Ldstr&&ins.Operand is string url&&url=="http://www.acquirespdl.jp/RTD/DLC/") ins.Operand="http://127.0.0.1:18765/";
}
if(args.Length>5&&args[5]=="local-api") {
 LocalPlatformPatch.Apply(asm);
 LocalBattleRestorePatch.Apply(asm);
 GzipBridge.Apply(fp,unity);
 var flow=AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[1])!,"RTDFlow.dll"));
 LocalStorePatch.Apply(asm,flow);
 NetworkTracePatch.Apply(asm,flow,log);
 var tick=mod.ImportReference(flow.MainModule.GetType("RTDFlow").Methods.First(m=>m.Name=="Tick"));
   foreach(var name in new[]{"TitleMenu","Installer","GameManager","MainMenu","MMResult","MMLogin","MMGachaCommon","MMGachaMenu","MMUnitDetail","MMUnitMenu","MMUnitPartyBase","MMUnitPartyList","MMUnitView","MMUnitSell","MMUnitUnionBase","MMUnitUnionList","MMUnitUnion","MMUnitEvoBase","MMUnitEvoList","MMUnitEvo","MMUnitSpCompBase","MMUnitSpCompList","MMUnitSpComp","MMQuest","MMEtcPresent","MMShop","MMShopAP","MMShopUnitSlot","MMShopFriend","MMEtcMenu","MMEtcChangeName","MMFriendMenu","MMFriendSearch","MMFriendList","MMFriendPost","MMFriendApply","MMFriendListSelPresent","MMHelper"}) {
  var method=mod.GetType(name).Methods.First(m=>m.Name=="Update");
  var il=method.Body.GetILProcessor();var first=method.Body.Instructions[0];
  il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0));il.InsertBefore(first,Instruction.Create(OpCodes.Call,tick));
 }
 int patched=0;
 foreach(var method in mod.GetType("ComManager").Methods.Where(m=>m.HasBody))
  foreach(var ins in method.Body.Instructions)
   if(ins.OpCode==OpCodes.Ldstr&&ins.Operand is string url&&url=="https://rtd.acquire-spgmsv.jp/rtd/2.4.0/api/") {
    ins.Operand="http://127.0.0.1:18766/rtd/2.4.0/api/";patched++;
   }
 if(patched!=2)throw new Exception("Expected both API root addresses; found "+patched);
 Console.WriteLine("Local API roots patched: "+patched);
}
fp.Write(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args[1])!,"Assembly-CSharp-firstpass-trace.dll"));
asm.Write(args[1]);Console.WriteLine(args[1]);

