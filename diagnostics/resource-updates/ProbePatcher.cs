using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class ProbePatcher {
 static void Main(string[] args) {
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(args[0]);
  var a=AssemblyDefinition.ReadAssembly(args[0]+"/Assembly-CSharp.dll",new ReaderParameters{AssemblyResolver=resolver});
  var helper=AssemblyDefinition.ReadAssembly(args[1]);
  var start=a.MainModule.ImportReference(helper.MainModule.GetType("RTDResourceProbe").Methods.Single(x=>x.Name=="StartProbe"));
  var awake=a.MainModule.GetType("AssetBundleLoader").Methods.Single(x=>x.Name=="Awake");
  awake.Body.GetILProcessor().InsertBefore(awake.Body.Instructions[0],Instruction.Create(OpCodes.Call,start));
  a.Write(args[2]);Console.WriteLine("probe hook written");
 }
}
