using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class LocalStorePatch {
 public static void Apply(AssemblyDefinition assembly,AssemblyDefinition flow) {
  var store=assembly.MainModule.GetType("StoreManager");
  foreach(var name in new[]{"m_BillingType","mb_Connecting","mb_StoreConnection","mb_InitSuccess","mb_StoreInit","mb_PaymentSuccess","mb_PaymentBegin","mb_GetProductData","mb_SkuCheckSuccess","mb_ConsumeSuccess","m_AndroidProductList","m_OriginalJson","m_PreOriginalJsonData","m_SignedData","m_PreSignedData","m_Signature","m_PreReceiptData","m_WaitProductID"})
   if(!store.Fields.Any(f=>f.Name==name))throw new Exception("Local store field missing: "+name);
  var com=assembly.MainModule.GetType("ComManager");
  if(!com.Fields.Any(f=>f.Name=="m_LastError")||!assembly.MainModule.GetType("Singleton`1").Fields.Any(f=>f.Name=="Instance"))throw new Exception("Local settlement singleton/error contract changed");
  foreach(var name in new[]{"EntryEvent","OnDestroy","ClearReceiptData"})Reset(store.Methods.Single(m=>m.Name==name)).Emit(OpCodes.Ret);
  Redirect(assembly,flow,"Start","Initialize",0);
  Redirect(assembly,flow,"Coroutin_Init","Init",0);
  Redirect(assembly,flow,"GetProductData","Products",0);
  Redirect(assembly,flow,"Coroutine_GetProduct","LoadProducts",0);
  Redirect(assembly,flow,"UpdateProductID","LoadProducts",0);
  Redirect(assembly,flow,"OpenPaymentDialog","Purchase",2);
  Redirect(assembly,flow,"Coroutin_ConsumeProduct","Consume",1);
  // The local receipt replaces GooglePurchase. The original UI otherwise skips
  // settlement because no real Google inventory object exists.
  foreach(var type in new[]{"MMShopQuartz","BattleShopMenu"}) {
   var update=assembly.MainModule.GetType(type).Methods.Single(m=>m.Name=="Update");
   var lookup=update.Body.Instructions.Single(i=>i.Operand is MethodReference r&&r.Name=="GetWaitPurchase");
   var branch=lookup.Next.Next.Next;
   if(branch.OpCode!=OpCodes.Brfalse)throw new Exception("Purchase inventory gate shape changed: "+type);
   branch.OpCode=OpCodes.Pop;branch.Operand=null;
  }
  var title=assembly.MainModule.GetType("TitleMenu").Methods.Single(m=>m.Name=="Update");
  var pending=title.Body.Instructions.Single(i=>i.Operand is MethodReference r&&r.Name=="GetWaitConsumeProduct");
  var pendingBranch=pending.Next.Next.Next;
  if(pendingBranch.OpCode!=OpCodes.Brfalse)throw new Exception("Title pending purchase gate shape changed");
  var titleIL=title.Body.GetILProcessor();
  titleIL.InsertBefore(pendingBranch,Instruction.Create(OpCodes.Pop));
  titleIL.InsertBefore(pendingBranch,Instruction.Create(OpCodes.Call,assembly.MainModule.ImportReference(flow.MainModule.GetType("RTDLocalStore").Methods.Single(m=>m.Name=="HasPending"))));
  Verify(assembly);
 }
 static ILProcessor Reset(MethodDefinition method) {
  method.Body=new MethodBody(method);return method.Body.GetILProcessor();
 }
 static void Redirect(AssemblyDefinition assembly,AssemblyDefinition flow,string source,string target,int parameters) {
  var method=assembly.MainModule.GetType("StoreManager").Methods.Single(m=>m.Name==source&&m.Parameters.Count==parameters);
  var callee=assembly.MainModule.ImportReference(flow.MainModule.GetType("RTDLocalStore").Methods.Single(m=>m.Name==target));
  var il=Reset(method);il.Emit(OpCodes.Ldarg_0);
  for(int i=0;i<parameters;i++)il.Emit(OpCodes.Ldarg,method.Parameters[i]);
  il.Emit(OpCodes.Call,callee);il.Emit(OpCodes.Ret);
 }
 public static void Verify(AssemblyDefinition assembly) {
  var store=assembly.MainModule.GetType("StoreManager");
  foreach(var name in new[]{"Start","Coroutin_Init","GetProductData","Coroutine_GetProduct","UpdateProductID","OpenPaymentDialog","Coroutin_ConsumeProduct"}) {
   var method=store.Methods.Single(m=>m.Name==name&&(name!="GetProductData"||m.Parameters.Count==0));
   var calls=method.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).ToArray();
   if(calls.Length!=1||calls[0].DeclaringType.FullName!="RTDLocalStore")throw new Exception("Local store bypass missing: "+name);
  }
  foreach(var name in new[]{"EntryEvent","OnDestroy","ClearReceiptData"})if(store.Methods.Single(m=>m.Name==name).Body.Instructions.Count!=1)throw new Exception("Store SDK event hook remains: "+name);
  foreach(var type in new[]{"MMShopQuartz","BattleShopMenu"}) {
   var update=assembly.MainModule.GetType(type).Methods.Single(m=>m.Name=="Update");
   var lookup=update.Body.Instructions.Single(i=>i.Operand is MethodReference r&&r.Name=="GetWaitPurchase");
   if(lookup.Next.Next.Next.OpCode!=OpCodes.Pop)throw new Exception("Local settlement still requires Google inventory: "+type);
  }
  var title=assembly.MainModule.GetType("TitleMenu").Methods.Single(m=>m.Name=="Update");
  var pending=title.Body.Instructions.Single(i=>i.Operand is MethodReference r&&r.Name=="GetWaitConsumeProduct");
  if(pending.Next.Next.Next.OpCode!=OpCodes.Pop||!(pending.Next.Next.Next.Next.Operand is MethodReference check)||check.DeclaringType.Name!="RTDLocalStore"||check.Name!="HasPending")throw new Exception("Title local receipt recovery missing");
  Console.WriteLine("Local store contract verified: catalog, initialization, purchase, settlement bypass platform SDK");
 }
}
