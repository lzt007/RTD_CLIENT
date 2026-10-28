using System;
using System.IO;
using System.Text;
if(args.Length!=4 || (args[0]!="apply" && args[0]!="verify"))throw new ArgumentException("apply|verify PARENT_DLL OUTPUT_OR_CANDIDATE_DLL REPORT");
string input=Path.GetFullPath(args[1]),candidatePath=Path.GetFullPath(args[2]),reportPath=Path.GetFullPath(args[3]);
var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
bool Same(string a,string b)=>string.Equals(a,b,comparison);
bool apply=args[0]=="apply";
if(Same(input,candidatePath)||Same(reportPath,input)||Same(reportPath,candidatePath)||File.Exists(reportPath)||Directory.Exists(reportPath))throw new InvalidDataException("Distinct new report destination required");
if(apply&&(File.Exists(candidatePath)||Directory.Exists(candidatePath)))throw new InvalidDataException("New output required");
var parent=File.ReadAllBytes(input);
var candidate=apply?NoticePatch.Patch(parent):File.ReadAllBytes(candidatePath);
var changed=NoticePatch.Verify(parent,candidate);
var report=Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(changed));
FileStream reportStream=null,outputStream=null;bool createdReport=false,createdOutput=false;
try {
 // Reserve both outputs exclusively; report can never truncate either DLL.
 reportStream=new FileStream(reportPath,FileMode.CreateNew,FileAccess.Write);createdReport=true;
 if(apply){outputStream=new FileStream(candidatePath,FileMode.CreateNew,FileAccess.Write);createdOutput=true;outputStream.Write(candidate);outputStream.Dispose();outputStream=null;}
 reportStream.Write(report);reportStream.Dispose();reportStream=null;
}catch {
 outputStream?.Dispose();reportStream?.Dispose();
 if(createdOutput)File.Delete(candidatePath);
 if(createdReport)File.Delete(reportPath);
 throw;
}
Console.WriteLine("Verified local notice: two managed bodies, three string sites");
