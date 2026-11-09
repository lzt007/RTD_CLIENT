using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class HoverPatcher {
 static void Main(string[] args) {
  if(args.Length!=3)throw new ArgumentException("managed-directory helper-dll output-assembly required");
  if(System.IO.Path.GetFullPath(args[0]+"/Assembly-CSharp.dll")==System.IO.Path.GetFullPath(args[2]))throw new ArgumentException("Refusing in-place patch");
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(args[0]);
  var a=AssemblyDefinition.ReadAssembly(args[0]+"/Assembly-CSharp.dll",new ReaderParameters{AssemblyResolver=resolver});
  var helper=AssemblyDefinition.ReadAssembly(args[1]);
  var start=a.MainModule.ImportReference(helper.MainModule.GetType("RTDHoverProbe").Methods.Single(x=>x.Name=="StartProbe"));
  var awake=a.MainModule.GetType("AssetBundleLoader").Methods.Single(x=>x.Name=="Awake");
  if(awake.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.Name=="RTDHoverProbe"))throw new InvalidOperationException("Already hooked");
  awake.Body.GetILProcessor().InsertBefore(awake.Body.Instructions[0],Instruction.Create(OpCodes.Call,start));
  a.Write(args[2]);Console.WriteLine("probe hook written");
 }
}
