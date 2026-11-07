using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 7)
    throw new ArgumentException("input-flow helper game unity output-flow report restore-flow-logs(0|1) required");
if (args[6] != "0" && args[6] != "1")
    throw new ArgumentException("restore-flow-logs must be 0 or 1");

using var original = AssemblyDefinition.ReadAssembly(args[0]);
using var helper = AssemblyDefinition.ReadAssembly(args[1]);
using var game = AssemblyDefinition.ReadAssembly(args[2]);
using var unity = AssemblyDefinition.ReadAssembly(args[3]);

// Refuse an unknown getter shape before introducing any reflected invocation.
var getter = game.MainModule.GetType("PlayerData")?.Methods.SingleOrDefault(
    m => m.Name == "GetEventPoint" && m.IsStatic && m.Parameters.Count == 0);
var getterBody = getter?.Body?.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToArray();
if (getterBody is not { Length: 2 } || getterBody[0].OpCode != OpCodes.Ldsfld ||
    getterBody[0].Operand is not FieldReference counter ||
    counter.DeclaringType.FullName != "PlayerData" || counter.Name != "SpEventPoint" ||
    counter.FieldType.FullName != "System.Int32" || getterBody[1].OpCode != OpCodes.Ret)
    throw new InvalidOperationException("GetEventPoint is not the reviewed pure counter getter");
if (helper.MainModule.AssemblyReferences.Any(a => a.Name != "mscorlib" && a.Name != "UnityEngine"))
    throw new InvalidOperationException("Probe references an unexpected runtime assembly");

var tick = original.MainModule.GetType("RTDFlow").Methods.Single(
    m => m.Name == "Tick" && m.IsStatic && m.Parameters.Count == 1 &&
         m.Parameters[0].ParameterType.FullName == "System.Object");
if (tick.Body.Instructions.Any(i => i.Operand is MethodReference m &&
        m.DeclaringType.FullName == "RTDPointProbe"))
    throw new InvalidOperationException("Duplicate probe injection refused");

int restoredLogs = args[6] == "1" ? RestoreLogs(original, unity) : 0;
var probe = helper.MainModule.GetType("RTDPointProbe").Methods.Single(
    m => m.Name == "Tick" && m.Parameters.Count == 1);
var il = tick.Body.GetILProcessor();
var first = tick.Body.Instructions[0];
il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
il.InsertBefore(first, il.Create(OpCodes.Call, original.MainModule.ImportReference(probe)));
original.Write(args[4]);

using var baseline = AssemblyDefinition.ReadAssembly(args[0]);
using var output = AssemblyDefinition.ReadAssembly(args[4]);
if (args[6] == "1") RestoreLogs(baseline, unity);
int verified = 0;
foreach (var type in baseline.MainModule.Types.SelectMany(Flatten))
foreach (var method in type.Methods)
{
    var outType = output.MainModule.GetType(type.FullName)
        ?? throw new InvalidOperationException("Original helper type removed");
    var outMethod = outType.Methods.Single(m => m.FullName == method.FullName);
    bool injected = type.FullName == "RTDFlow" && method.Name == "Tick" && method.Parameters.Count == 1;
    if (Shape(method, false) != Shape(outMethod, injected))
        throw new InvalidOperationException("Original helper IL changed: " + method.FullName);
    verified++;
}
var loadedTick = output.MainModule.GetType("RTDFlow").Methods.Single(
    m => m.Name == "Tick" && m.Parameters.Count == 1);
if (loadedTick.Body.Instructions[0].OpCode != OpCodes.Ldarg_0 ||
    loadedTick.Body.Instructions[1].Operand is not MethodReference added ||
    added.DeclaringType.FullName != "RTDPointProbe")
    throw new InvalidOperationException("Probe hook missing");

var result = new
{
    injected = "RTDFlow.Tick(object) -> RTDPointProbe.Tick(object)",
    original_methods_structurally_verified = verified,
    original_exception_regions_unchanged = true,
    restored_existing_ui_log_calls = restoredLogs,
    pure_counter_getter_verified = true,
    probe_runtime_references = helper.MainModule.AssemblyReferences.Select(a => a.FullName).ToArray(),
    original_gameplay_assembly_modified = false
};
File.WriteAllText(args[5], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Helper IL, pure getter, and original exception regions verified.");

static int RestoreLogs(AssemblyDefinition assembly, AssemblyDefinition unity)
{
    var log = assembly.MainModule.ImportReference(unity.MainModule.GetType("UnityEngine.Debug")
        .Methods.Single(m => m.Name == "Log" && m.Parameters.Count == 1 &&
            m.Parameters[0].ParameterType.FullName == "System.Object"));
    var method = assembly.MainModule.GetType("RTDFlow").Methods.Single(m => m.Name == "LogObservation");
    int count = 0;
    foreach (var instruction in method.Body.Instructions)
    {
        if (instruction.OpCode != OpCodes.Pop || instruction.Previous?.Operand is not MethodReference previous ||
            previous.DeclaringType.FullName != "System.String" || previous.Name != "Concat") continue;
        instruction.OpCode = OpCodes.Call;
        instruction.Operand = log;
        count++;
    }
    if (count != 2) throw new InvalidOperationException("Expected exactly two suppressed UI log calls");
    return count;
}

static string Shape(MethodDefinition method, bool stripProbe)
{
    var instructions = method.HasBody
        ? method.Body.Instructions.Skip(stripProbe ? 2 : 0).ToArray()
        : Array.Empty<Instruction>();
    string Operand(object? value) => value switch
    {
        Instruction instruction => "@" + Array.IndexOf(instructions, instruction),
        Instruction[] list => string.Join(",", list.Select(i => "@" + Array.IndexOf(instructions, i))),
        null => "",
        _ => value.ToString() ?? ""
    };
    string body = string.Join("\n", instructions.Select(i => i.OpCode.Code + " " + Operand(i.Operand)));
    if (method.HasBody)
        body += "\nEH " + string.Join(";", method.Body.ExceptionHandlers.Select(e =>
            e.HandlerType + ":" + Operand(e.TryStart) + ":" + Operand(e.TryEnd) + ":" +
            Operand(e.HandlerStart) + ":" + Operand(e.HandlerEnd) + ":" +
            Operand(e.FilterStart) + ":" + e.CatchType?.FullName));
    return body;
}

static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes)
    foreach (var item in Flatten(nested)) yield return item;
}
