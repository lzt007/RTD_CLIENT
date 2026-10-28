using System;
using System.IO;
if(args.Length!=4||args[0]!="apply")throw new ArgumentException("apply PARENT_DLL HELPER_DLL OUTPUT_DLL");
string parent=Path.GetFullPath(args[1]),helper=Path.GetFullPath(args[2]),output=Path.GetFullPath(args[3]);
if(output==parent||output==helper||File.Exists(output))throw new InvalidDataException("Output must be a new derivative file");
byte[] result=RestorePatch.Patch(File.ReadAllBytes(parent),File.ReadAllBytes(helper));
File.WriteAllBytes(output,result);Console.WriteLine("Verified four restore-cache hooks; no original game assembly executed");
