using System;
using Mono.Cecil;
class RetargetTest {static void Main(string[] a){var asm=AssemblyDefinition.ReadAssembly(a[0]);foreach(var r in asm.MainModule.AssemblyReferences)if(r.Name=="mscorlib"||r.Name=="System"||r.Name=="System.Core"){r.Version=new Version(4,0,0,0);r.PublicKeyToken=new byte[]{0xb7,0x7a,0x5c,0x56,0x19,0x34,0xe0,0x89};}asm.Write(a[1]);}}
