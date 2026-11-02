using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class MonoBehaviour { public object StartCoroutine(IEnumerator e) { while(e.MoveNext()){} return null; } }
 public static class PlayerPrefs {
  static Dictionary<string,string> values=new Dictionary<string,string>();
  public static string GetString(string k,string fallback) { string v;return values.TryGetValue(k,out v)?v:fallback; }
  public static void SetString(string k,string v){values[k]=v;}
  public static void DeleteKey(string k){values.Remove(k);}public static void Save(){}
 }
}
class Row {public string id="rtd_item_10010",name="six";public int price=450,point=6;}
class ClientData {public static Row[] rows=new[]{new Row()};public static Row[] GetShopProductMaster(){return rows;}}
class AndroidProduct {public string id{get;private set;}public AndroidProduct(string i,string n,int p,int point,string text){id=i;}}
class Singleton<T> where T:new(){public static T Instance=new T();}
class ComManager:Singleton<ComManager> {
 public static int m_LastError=0;public string receipt;
 public IEnumerator GooglePlayBillPoint(string data,string signature,string original){receipt=original;yield break;}
}
class Store:MonoBehaviour {
 public enum Billing {Old,New} public Billing m_BillingType;
 public bool mb_Connecting,mb_StoreConnection,mb_InitSuccess,mb_StoreInit,mb_PaymentSuccess,mb_PaymentBegin,mb_GetProductData,mb_SkuCheckSuccess,mb_ConsumeSuccess;
 public string m_OriginalJson,m_PreOriginalJsonData,m_SignedData,m_PreSignedData,m_Signature,m_PreReceiptData,m_WaitProductID;
 public AndroidProduct[] m_AndroidProductList;
}
class LocalStoreTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Run(IEnumerator e){while(e.MoveNext()){}}
 public static void Main(){
  ClientData.rows=null;var loading=new Store();RTDLocalStore.Initialize(loading);Check(!RTDLocalStore.ProductsReady(loading),"wait for master data");ClientData.rows=new[]{new Row()};Check(RTDLocalStore.ProductsReady(loading),"retry once masters arrive");
  var store=new Store();Check(!RTDLocalStore.HasPending(),"no startup settlement without receipt");RTDLocalStore.Initialize(store);Check(RTDLocalStore.ProductsReady(store),"first shop entry must load products without SDK callback");
  Check(store.mb_InitSuccess&&store.mb_SkuCheckSuccess&&store.m_AndroidProductList.Length==1,"offline catalog");
  RTDLocalStore.Purchase(store,0,0);string receipt=store.m_OriginalJson;
  Check(RTDLocalStore.HasPending(),"startup settlement detects local receipt");
  Check(receipt.Contains("rtd_item_10010")&&receipt.Contains("local-"),"local receipt");
  RTDLocalStore.Purchase(store,0,0);Check(store.m_OriginalJson==receipt,"retry must keep transaction");
  RTDLocalStore.Clear(store);Check(store.m_OriginalJson==receipt,"unsettled receipt must survive UI clear");
  var restarted=new Store();RTDLocalStore.Initialize(restarted);
  Check(restarted.mb_PaymentBegin&&restarted.m_OriginalJson==receipt&&restarted.m_WaitProductID.Substring(18)=="10010","restart recovery");
  ComManager.m_LastError=1;Run(RTDLocalStore.Consume(restarted,null));
  Check(!restarted.mb_ConsumeSuccess&&restarted.m_OriginalJson==receipt,"failed settlement retains receipt");
  ComManager.m_LastError=0;Run(RTDLocalStore.Consume(restarted,null));
  Check(restarted.mb_ConsumeSuccess&&!restarted.mb_Connecting&&ComManager.Instance.receipt==receipt,"settle through original protocol");
  Check(restarted.m_OriginalJson==null,"successful receipt cleared");
  Check(!RTDLocalStore.HasPending(),"startup must skip consumed receipt");
  Check(!restarted.mb_PaymentBegin&&restarted.m_WaitProductID==null,"success must not reenter recovery");
  RTDLocalStore.Products(restarted);RTDLocalStore.Purchase(restarted,0,0);
  Check(restarted.m_OriginalJson!=receipt,"next purchase gets new transaction");
  Console.WriteLine("Local store tests passed: catalog, retry, restart, failed/successful settlement, next purchase");
 }
}
