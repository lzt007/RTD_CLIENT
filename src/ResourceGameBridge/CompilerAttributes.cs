#if RTD_LEGACY
// Compiler metadata absent from the original stripped Unity mscorlib.
namespace System.Runtime.CompilerServices {
 [System.AttributeUsage(System.AttributeTargets.Assembly)]
 public sealed class CompilationRelaxationsAttribute:System.Attribute {public CompilationRelaxationsAttribute(int value){} }
 [System.AttributeUsage(System.AttributeTargets.Assembly)]
 public sealed class RuntimeCompatibilityAttribute:System.Attribute {public bool WrapNonExceptionThrows {get;set;} }
}
#endif
