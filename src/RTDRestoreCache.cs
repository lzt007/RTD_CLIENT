// Runtime repair only. The separate RTDFlow observer remains read-only.
using System;
using System.Collections;
using System.Collections.Generic;
using CommunicationRTD;
using UnityEngine;

#if RTD_LEGACY
// The original stripped mscorlib omits these compiler-only attributes, as in
// the already accepted separate observer. They do not alter game behavior.
namespace System.Runtime.CompilerServices {
 [AttributeUsage(AttributeTargets.Assembly)]
 public sealed class CompilationRelaxationsAttribute:Attribute {public CompilationRelaxationsAttribute(int value){} }
 [AttributeUsage(AttributeTargets.Assembly)]
 public sealed class RuntimeCompatibilityAttribute:Attribute {public bool WrapNonExceptionThrows {get;set;} }
}
#endif

public static class RTDRestoreCache {
 enum Stage {Idle,Armed,Loading,Ready,Failed}
 static Stage stage;
 static int generation;
 static MonoBehaviour activeOwner;

 public static void Reset(){generation++;stage=Stage.Idle;activeOwner=null;}
 public static void MarkRestore(){generation++;stage=Stage.Armed;activeOwner=null;}
 public static void BeforeBattleExit(bool won){if(!won)Reset();}

 public static bool BeforeResult(MonoBehaviour owner,int mode,int current,int count) {
  // Quest books also set RESULT_MODE. This hook is on MMResult, not gacha UI.
  if(stage==Stage.Idle||mode!=1001||count<=0||current!=count-1)return false;
  if(stage!=Stage.Armed&&!object.ReferenceEquals(owner,activeOwner)) {
   generation++;Fail(generation,"Result owner changed");return true;
  }
  if(stage==Stage.Ready){stage=Stage.Idle;activeOwner=null;return false;}
  if(stage==Stage.Failed||stage==Stage.Loading)return true;
  activeOwner=owner;stage=Stage.Loading;
  try {
   if(owner==null)throw new InvalidOperationException("Missing result owner");
   ComManager com=Singleton<ComManager>.Instance;
   if(com==null)throw new InvalidOperationException("Missing original ComManager");
   // Original ComManager survives scene changes. Keep request outer iterators
   // alive there so they can release their sequence lock after owner destruction.
   com.StartCoroutine(new Guarded(Refresh(owner,com,generation),generation));
  }catch(Exception e){Fail(generation,"Cannot start cache refresh: "+e.Message);}
  return true;
 }

 static bool Current(MonoBehaviour owner,int version) {
  return owner!=null&&version==generation&&stage==Stage.Loading&&object.ReferenceEquals(owner,activeOwner);
 }
 static void Fail(int version,string why) {
  if(version!=generation)return;
  stage=Stage.Failed;Debug.LogError("RTD_RESTORE_CACHE blocked: "+why);
 }
 static IEnumerator Refresh(MonoBehaviour owner,ComManager com,int version) {
  while(Current(owner,version)&&!ComManager.IsEnd())yield return null;
  if(!Current(owner,version))yield break;
  if(ComManager.GetReturnCode()!=PROC_CODE.OK){Fail(version,"Prior communication failed");yield break;}
  yield return com.StartCoroutine(new Guarded(com.GetUnits(),version));
  if(!Current(owner,version))yield break;
  if(!ComManager.IsEnd()||ComManager.GetReturnCode()!=PROC_CODE.OK||!CanonicalUnits()) {
   Fail(version,"GetUnits did not restore canonical inventory and parties");yield break;
  }
  yield return com.StartCoroutine(new Guarded(com.Login(),version));
  if(!Current(owner,version))yield break;
  if(!ComManager.IsEnd()||ComManager.GetReturnCode()!=PROC_CODE.OK||ClientData.m_PlayerLogin==null) {
   Fail(version,"Login did not refresh the original login cache");yield break;
  }
  stage=Stage.Ready;Debug.Log("RTD_RESTORE_CACHE ready: original GetUnits/Login completed");
 }

 static bool CanonicalUnits() {
  var party=ClientData.m_PartyInfo;var units=ClientData.m_PlayerUnits;
  if(party==null||party.party==null||party.party.Length!=5||party.active>=5||units==null)return false;
  var owned=new Dictionary<ulong,bool>();
  foreach(var unit in units) {
   if(unit==null||unit.instance_id==0||owned.ContainsKey(unit.instance_id))return false;
   owned.Add(unit.instance_id,true);
  }
  foreach(var group in party.party) {
   if(group.instanceIds==null||group.instanceIds.Length!=5)return false;
   foreach(ulong id in group.instanceIds)if(id!=0&&!owned.ContainsKey(id))return false;
  }
  return party.party[party.active].instanceIds[0]!=0;
 }

 sealed class Guarded:IEnumerator {
  readonly IEnumerator inner;readonly int version;
  public Guarded(IEnumerator value,int currentVersion){inner=value;version=currentVersion;}
  public object Current {get{return inner.Current;}}
  public bool MoveNext() {
   // Drain an already started original request even after cancellation so its
   // ComManager sequence can release normally; Refresh checks identity after it.
   try{return inner.MoveNext();}catch(Exception e){Fail(version,"Original refresh coroutine failed: "+e.Message);return false;}
  }
  public void Reset(){throw new NotSupportedException();}
 }
}
