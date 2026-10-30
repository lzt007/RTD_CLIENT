using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class LogLevelPatch {
 public static void Apply(string path,string level) {
  int minimum=Array.IndexOf(new[]{"debug","info","warning","error","none"},level);
  if(minimum<0)throw new ArgumentException("Invalid logging level: "+level);
  using var assembly=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{InMemory=true});int removed=0;
  foreach(var type in assembly.MainModule.GetTypes())
  foreach(var method in type.Methods.Where(m=>m.HasBody))
  foreach(var instruction in method.Body.Instructions.ToArray()) {
   if(instruction.OpCode!=OpCodes.Call||instruction.Operand is not MethodReference target||target.DeclaringType.FullName!="UnityEngine.Debug")continue;
   int severity=target.Name switch {"Log" or "LogFormat"=>1,"LogWarning" or "LogWarningFormat"=>2,"LogError" or "LogErrorFormat" or "LogException" or "Assert"=>3,_=>-1};
   if(severity<0||severity>=minimum||target.ReturnType.FullName!="System.Void"||target.HasThis)continue;
   // Keep argument evaluation and branch targets intact; consume the original arguments.
   instruction.OpCode=target.Parameters.Count==0?OpCodes.Nop:OpCodes.Pop;instruction.Operand=null;
   var cursor=instruction;var il=method.Body.GetILProcessor();
   for(int i=1;i<target.Parameters.Count;i++){var pop=Instruction.Create(OpCodes.Pop);il.InsertAfter(cursor,pop);cursor=pop;}
   removed++;
  }
  assembly.Write(path);Console.WriteLine("Logging "+level+": suppressed "+removed+" calls in "+path);
 }
}
