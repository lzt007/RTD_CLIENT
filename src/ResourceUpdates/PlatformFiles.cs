using System;
using System.IO;
using System.Text;
#if RTD_LEGACY
using UnityEngine;
#else
using System.Security.Cryptography;
#endif
namespace RTD.ResourceUpdates {
 public static class ResourceFiles {
#if RTD_LEGACY
  static int NativeOpen(string path,int flags){throw new NotSupportedException("Native resource IO patch missing");}
  static int NativeSync(int fd){throw new NotSupportedException("Native resource IO patch missing");}
  static int NativeClose(int fd){throw new NotSupportedException("Native resource IO patch missing");}
#endif
  public static void SyncFile(string path){
#if RTD_LEGACY
   using(AndroidJavaObject file=new AndroidJavaObject("java.io.RandomAccessFile",path,"rw"))
   using(AndroidJavaObject descriptor=file.Call<AndroidJavaObject>("getFD")){
    try{descriptor.Call("sync");}finally{file.Call("close");}
   }
#endif
  }
  public static void SyncDirectory(string path){
#if RTD_LEGACY
   int fd=NativeOpen(path,0);if(fd<0)throw new IOException("Cannot open resource directory for sync");
   try{if(NativeSync(fd)!=0)throw new IOException("Cannot sync resource directory");}finally{NativeClose(fd);}
#endif
  }

  public static long Length(string path){using(Stream file=File.OpenRead(path))return file.Length;}
  public static string ReadText(string path){byte[] bytes=File.ReadAllBytes(path);return Encoding.UTF8.GetString(bytes,0,bytes.Length);}
  public static void Move(string from,string to){
#if RTD_LEGACY
   using(AndroidJavaObject source=new AndroidJavaObject("java.io.File",from))
   using(AndroidJavaObject target=new AndroidJavaObject("java.io.File",to))
    if(!source.Call<bool>("renameTo",target))throw new IOException("Resource rename failed");
#else
   if(Directory.Exists(from))Directory.Move(from,to);else File.Move(from,to);
#endif
   SyncDirectory(Path.GetDirectoryName(from));if(Path.GetDirectoryName(from)!=Path.GetDirectoryName(to))SyncDirectory(Path.GetDirectoryName(to));
  }
  public static void RemoveDirectory(string path){
#if RTD_LEGACY
   using(AndroidJavaObject directory=new AndroidJavaObject("java.io.File",path)) {
    string[] children=directory.Call<string[]>("list");
    if(children==null)throw new IOException("Cannot list resource directory");
    foreach(string name in children){string child=Path.Combine(path,name);if(Directory.Exists(child))RemoveDirectory(child);else File.Delete(child);}
    if(!directory.Call<bool>("delete"))throw new IOException("Cannot delete resource directory");
   }
#else
   Directory.Delete(path,true);
#endif
  }
  public static string[] Manifests(string path){
#if RTD_LEGACY
   System.Collections.Generic.List<string> result=new System.Collections.Generic.List<string>();
   using(AndroidJavaObject directory=new AndroidJavaObject("java.io.File",path)){
    string[] names=directory.Call<string[]>("list");if(names==null)throw new IOException("Cannot list resource history");
    foreach(string name in names)if(name.EndsWith(".manifest")&&File.Exists(Path.Combine(path,name)))result.Add(Path.Combine(path,name));
   }
   return result.ToArray();
#else
   return Directory.GetFiles(path,"*.manifest");
#endif
  }
  public static string Hash(Stream input){
   byte[] bytes;
#if RTD_LEGACY
   using(AndroidJavaClass digestClass=new AndroidJavaClass("java.security.MessageDigest"))
   using(AndroidJavaObject digest=digestClass.CallStatic<AndroidJavaObject>("getInstance","SHA-256")){
    byte[] buffer=new byte[1048576];int read;
    while((read=input.Read(buffer,0,buffer.Length))>0)digest.Call("update",buffer,0,read);
    bytes=digest.Call<byte[]>("digest");
   }
#else
   using(SHA256 hash=SHA256.Create())bytes=hash.ComputeHash(input);
#endif
   const string digits="0123456789abcdef";char[] result=new char[bytes.Length*2];for(int i=0;i<bytes.Length;i++){result[i*2]=digits[bytes[i]>>4];result[i*2+1]=digits[bytes[i]&15];}return new string(result);
  }
 }
}
