using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(args[2]);
using var a=AssemblyDefinition.ReadAssembly(args[0],new ReaderParameters{AssemblyResolver=resolver});
using var helper=AssemblyDefinition.ReadAssembly(args[1]);var mod=a.MainModule;
MethodReference Hook(string n)=>mod.ImportReference(helper.MainModule.GetType("RTDLocalization").Methods.Single(m=>m.Name==n));
var process=mod.GetType("UILabel").Methods.Single(m=>m.Name=="ProcessText");var loads=process.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldfld&&i.Operand is FieldReference f&&f.Name=="mText").ToArray();
// First mText load stores mLastText and must retain business/cache identity; second supplies Replace.
if(loads.Length!=2||loads[1].Next?.OpCode!=OpCodes.Ldstr)throw new Exception("Unexpected UILabel.ProcessText shape");
var il=process.Body.GetILProcessor();il.InsertBefore(loads[1].Previous,Instruction.Create(OpCodes.Ldarg_0));il.InsertAfter(loads[1],Instruction.Create(OpCodes.Call,Hook("Display")));
var input=mod.GetType("UIInput").Methods.Single(m=>m.Name=="Init");foreach(var ret in input.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){ret.OpCode=OpCodes.Ldarg_0;input.Body.GetILProcessor().InsertAfter(ret,Instruction.Create(OpCodes.Call,Hook("ProtectInput")));input.Body.GetILProcessor().InsertAfter(ret.Next,Instruction.Create(OpCodes.Ret));}
int templates=0;foreach(var type in mod.Types)foreach(var m in type.Methods.Where(m=>m.HasBody))foreach(var call in m.Body.Instructions.ToArray()){
 if(call.Operand is not MethodReference mr||mr.DeclaringType.Name!="GameText"||mr.Name!="Get")continue;
 // Only a single basic block, direct GameText->String.Format; do not change GameText getter or non-display consumers.
 var next=call.Next;int limit=0,depth=1;bool format=false;while(next!=null&&limit++<80){
 if(next.OpCode.FlowControl==FlowControl.Branch||next.OpCode.FlowControl==FlowControl.Cond_Branch||next.OpCode.FlowControl==FlowControl.Return||next.OpCode.FlowControl==FlowControl.Throw)break;
 int pop=next.OpCode.StackBehaviourPop==StackBehaviour.Pop0?0:next.OpCode.StackBehaviourPop.ToString().Split('_').Length;
 int push=next.OpCode.StackBehaviourPush==StackBehaviour.Push0?0:next.OpCode.StackBehaviourPush.ToString().Split('_').Length;
 if(next.Operand is MethodReference target){pop=target.Parameters.Count+(target.HasThis&&next.OpCode!=OpCodes.Newobj?1:0);push=next.OpCode==OpCodes.Newobj||target.ReturnType.FullName!="System.Void"?1:0;
 if(target.DeclaringType.FullName=="System.String"&&target.Name=="Format"&&pop==depth&&target.Parameters[0].ParameterType.FullName=="System.String"){format=true;break;}}
 if(pop>=depth)break;depth+=push-pop;next=next.Next;
 }
 if(format){var target=(MethodReference)next.Operand;var hook=helper.MainModule.GetType("RTDLocalization").Methods.SingleOrDefault(h=>h.Name=="Format"&&h.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(target.Parameters.Select(p=>p.ParameterType.FullName)));if(hook==null)throw new Exception("Unsupported template format overload "+target.FullName);next.OpCode=OpCodes.Call;next.Operand=mod.ImportReference(hook);templates++;}
}
a.Write(args[3]);Console.WriteLine("Localization hooks: UILabel.ProcessText=1 UIInput.Init=1 formatTemplates="+templates);
