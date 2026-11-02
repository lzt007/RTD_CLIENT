using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

// Linked into RTDFlow. Reflection keeps this helper independent of Assembly-CSharp.
public static class RTDLocalStore {
 const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 const string ReceiptKey="RTD_LOCAL_PURCHASE_V1";
 static FieldInfo Field(object obj,string name) { return obj.GetType().GetField(name,Flags); }
 static void Set(object obj,string name,object value) { Field(obj,name).SetValue(obj,value); }
 static object Get(object obj,string name) { return Field(obj,name).GetValue(obj); }
 static IEnumerator Empty() { yield break; }
 public static bool HasPending() { return PlayerPrefs.GetString(ReceiptKey,"").Length>0; }
 public static void Initialize(object store) {
  Set(store,"m_BillingType",Enum.ToObject(Field(store,"m_BillingType").FieldType,1));
  Set(store,"mb_Connecting",false);Set(store,"mb_StoreConnection",false);
  Set(store,"mb_InitSuccess",true);Set(store,"mb_StoreInit",true);
  string receipt=PlayerPrefs.GetString(ReceiptKey,"");
  if(receipt.Length>0) { SetReceipt(store,receipt);Set(store,"mb_PaymentSuccess",true);Set(store,"mb_PaymentBegin",true); }
 }
 public static IEnumerator Init(object store) { Initialize(store);return Empty(); }
 public static bool ProductsReady(object store) {
  if(!(bool)Get(store,"mb_GetProductData"))Products(store);
  return (bool)Get(store,"mb_GetProductData");
 }
 public static void Products(object store) {
  Type client=store.GetType().Assembly.GetType("ClientData");
  Array rows=(Array)client.GetMethod("GetShopProductMaster",Flags).Invoke(null,null);
  if(rows==null||rows.Length==0) { Set(store,"mb_GetProductData",false);Set(store,"mb_SkuCheckSuccess",false);return; }
  Type product=store.GetType().Assembly.GetType("AndroidProduct");
  Array list=Array.CreateInstance(product,rows==null?0:rows.Length);
  for(int i=0;i<list.Length;i++) {
   object row=rows.GetValue(i);
   list.SetValue(CreateProduct(product,row),i);
  }
  Set(store,"m_AndroidProductList",list);Set(store,"mb_GetProductData",true);
  Set(store,"mb_SkuCheckSuccess",list.Length>0);Set(store,"mb_Connecting",false);
 }
 public static object CreateProduct(Type product,object row) {
  return Activator.CreateInstance(product,new object[]{Get(row,"id"),"SIM / "+Get(row,"name"),0,Convert.ToInt32(Get(row,"point")),"LOCAL SIMULATION - NO PAYMENT"});
 }
 public static IEnumerator LoadProducts(object store) { Products(store);return Empty(); }
 static void SetReceipt(object store,string receipt) {
  Set(store,"m_OriginalJson",receipt);Set(store,"m_PreOriginalJsonData",receipt);
  int begin=receipt.IndexOf("rtd_item_");
  // The original recovery UI parses the numeric suffix at offset 18 (iOS format).
  if(begin>=0)Set(store,"m_WaitProductID","jp.co.acquire.rtd_"+receipt.Substring(begin+9,5));
  Set(store,"m_SignedData",receipt);Set(store,"m_PreSignedData",receipt);
  Set(store,"m_Signature","local-simulation");
 }
 public static void Purchase(object store,int index,int unused) {
  // Keep an unsettled transaction across retries/restarts; never replace it with a new ID.
  string receipt=PlayerPrefs.GetString(ReceiptKey,"");
  if(receipt.Length==0) {
   Array products=(Array)Get(store,"m_AndroidProductList");
   object product=products.GetValue(index);
   string id=(string)product.GetType().GetProperty("id",Flags).GetValue(product,null);
   bool known=false;foreach(string sku in new[]{"rtd_item_10000","rtd_item_10010","rtd_item_10020","rtd_item_10030","rtd_item_10040","rtd_item_10050"})if(id==sku)known=true;
   if(!known)throw new InvalidOperationException("Unknown local product");
   receipt="{\"productId\":\""+id+"\",\"orderId\":\"local-"+Guid.NewGuid().ToString("N")+"\"}";
   PlayerPrefs.SetString(ReceiptKey,receipt);PlayerPrefs.Save();
  }
  SetReceipt(store,receipt);Set(store,"mb_StoreConnection",false);Set(store,"mb_PaymentSuccess",true);
 }
 public static IEnumerator Consume(object store,object ignored) {
  Set(store,"mb_Connecting",true);Set(store,"mb_ConsumeSuccess",false);
  Type com=store.GetType().Assembly.GetType("ComManager");
  object instance=com.BaseType.GetField("Instance",Flags|BindingFlags.FlattenHierarchy).GetValue(null);
  IEnumerator request=(IEnumerator)com.GetMethod("GooglePlayBillPoint",Flags).Invoke(instance,new object[]{Get(store,"m_SignedData"),Get(store,"m_Signature"),Get(store,"m_OriginalJson")});
  yield return ((MonoBehaviour)store).StartCoroutine(request);
  bool success=Convert.ToInt32(com.GetField("m_LastError",Flags).GetValue(null))==0;
  Set(store,"mb_ConsumeSuccess",success);Set(store,"mb_Connecting",false);
  if(success) {
   PlayerPrefs.DeleteKey(ReceiptKey);PlayerPrefs.Save();Clear(store);
   Set(store,"mb_PaymentBegin",false);Set(store,"m_WaitProductID",null);
  }
 }
 public static void Clear(object store) {
  // UI may clear old SDK preferences, but an unsettled local receipt must survive.
  if(PlayerPrefs.GetString(ReceiptKey,"").Length>0)return;
  foreach(string key in new[]{"PRE_RECEIPT","PRE_SIGNED","PRE_JSON"})PlayerPrefs.DeleteKey(key);
  foreach(string name in new[]{"m_PreSignedData","m_PreReceiptData","m_PreOriginalJsonData","m_SignedData","m_Signature","m_OriginalJson"})Set(store,name,null);
 }
}
