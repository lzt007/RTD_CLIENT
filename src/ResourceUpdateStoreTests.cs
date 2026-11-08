using System;
using System.IO;
using RTD.ResourceUpdates;
class ResourceUpdateStoreTests {
 static int count;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);count++;}
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"rtd-store-"+Guid.NewGuid().ToString("N"));
  try {
   ResourceStore store=new ResourceStore(root);
   Check(store.Recover()==null,"fresh store has no rollback");
   byte[] old=System.Text.Encoding.UTF8.GetBytes("old");
   byte[] fresh=System.Text.Encoding.UTF8.GetBytes("new");
   string oldHash="cba06b5736faf67e54b07b561eae94395e774c517a7d910a54369e1263ccfbd4";
   string newHash="11507a0e2f5e69d5dfa40a62a1bd7b6ee57e6bcd85c67c9b8431b36fff21c437";
   store.PutVerified(oldHash,3,old);
   bool rejected=false;try{store.PutVerified(newHash,3,old);}catch(IOException){rejected=true;}
   Check(rejected&&!store.HasVerified(newHash,3),"bad hash never ready");
   store.Stage("old",new string[]{oldHash},"old-manifest");store.Commit();store.Accept();
   Check(new ResourceStore(root).Recover()=="old-manifest","accepted version survives restart");
   store.PutVerified(newHash,3,fresh);store.Stage("new",new string[]{newHash},"new-manifest");
   Check(new ResourceStore(root).Recover()=="old-manifest","staged crash restores old");
   store.Stage("new",new string[]{newHash},"new-manifest");store.Commit();
   Check(new ResourceStore(root).Recover()=="old-manifest","unaccepted activation restores old");
   store.Stage("new",new string[]{newHash},"new-manifest");store.Commit();store.Accept();
   Check(new ResourceStore(root).Recover()=="new-manifest","accepted activation survives");
   Check(store.ReadHistory().Length==2,"accepted generations retained for other operation fallback");
   store.Stage("interrupted",new string[]{newHash},"interrupted-manifest");
   File.WriteAllText(Path.Combine(root,"journal"),"activating");
   Check(store.Rollback()=="new-manifest","failure after journal before directory switch preserves accepted active");
   store.Stage("interrupted",new string[]{newHash},"interrupted-manifest");
   File.WriteAllText(Path.Combine(root,"journal"),"activating");
   if(Directory.Exists(Path.Combine(root,"previous")))Directory.Delete(Path.Combine(root,"previous"),true);
   Directory.Move(Path.Combine(root,"active"),Path.Combine(root,"previous"));
   Check(store.Rollback()=="new-manifest","failure between directory renames restores previous accepted active");
   store.Stage("interrupted",new string[]{newHash},"interrupted-manifest");store.Commit();
   File.WriteAllText(Path.Combine(root,"active/accepted"),"accepted");
   Check(store.Rollback()=="new-manifest","explicit rollback after acceptance marker sync failure restores old generation");
   File.WriteAllBytes(store.BlobPath(newHash),old);
   Check(!store.HasVerified(newHash,3),"same length corrupt bytes detected");
   rejected=false;try{store.Stage("bad",new string[]{newHash},"bad");}catch(IOException){rejected=true;}
   Check(rejected,"corrupt dependency blocks whole stage");
   store.PutVerified(newHash,3,fresh);store.Stage("new",new string[]{newHash},"new-manifest");store.Commit();store.Accept();
   Check(store.ReadHistory().Length==2,"unchanged checks must not grow generation history");
   Directory.CreateDirectory(Path.Combine(root,"history/00000000000000000003.manifest"));
   store.Stage("third",new string[]{newHash},"third-manifest");store.Commit();store.Accept();
   Check(store.Warning!=null&&new ResourceStore(root).Recover()=="third-manifest"&&store.ReadHistory()[0]=="third-manifest","accepted active generation remains first fallback after history cleanup failure");
   Console.WriteLine("PASS "+count+" resource store tests");
  } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
