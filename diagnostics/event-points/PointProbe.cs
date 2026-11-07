// Diagnostic only: fields + one documented pure getter; never writes game state.
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace System.Runtime.CompilerServices {
 [AttributeUsage(AttributeTargets.Assembly)] public sealed class CompilationRelaxationsAttribute : Attribute { public CompilationRelaxationsAttribute(int v) {} }
 [AttributeUsage(AttributeTargets.Assembly)] public sealed class RuntimeCompatibilityAttribute : Attribute { public bool WrapNonExceptionThrows {get;set;} }
}
public static class RTDPointProbe {
 const BindingFlags Static=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
 const BindingFlags Instance=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly;
 static Assembly game;
 static string previous, lastError, unlocks="[]";
 static float sampledAt=-1f;
 static object Field(object value,string name) {
  if(value==null)return null;
  for(Type t=value.GetType();t!=null;t=t.BaseType) { FieldInfo f=t.GetField(name,Instance); if(f!=null)return f.GetValue(value); }
  return null;
 }
 static object StaticField(Type t,string name) { if(t==null)return null;FieldInfo f=t.GetField(name,Static);return f==null?null:f.GetValue(null); }
 static int Int(object value) { return value==null?-1:Convert.ToInt32(value); }
 static string Bool(object value) { return value==null?"null":(Convert.ToBoolean(value)?"true":"false"); }
 static bool Target(int id) { return id==20710||id==20711||id==20712; }
 static void ReadUnlocks(object source) {
  if(source.GetType().Name!="MMQuest")return;
  IEnumerable list=Field(source,"m_List") as IEnumerable;
  if(list==null)return;
  List<string> rows=new List<string>();
  foreach(object row in list) {
   int id=Int(Field(Field(row,"m_Item"),"id")); if(!Target(id))continue;
   rows.Add("{\"quest_id\":"+id+",\"unlocked\":"+Bool(Field(row,"m_SpEventUnlock"))+"}");
  }
  rows.Sort();
  StringBuilder s=new StringBuilder("[");for(int i=0;i<rows.Count;i++){if(i>0)s.Append(',');s.Append(rows[i]);}s.Append(']');unlocks=s.ToString();
 }
 public static void Tick(object source) {
  try {
   if(source==null)return;
   if(game==null) { Assembly a=source.GetType().Assembly;if(a.GetType("ClientData")==null)return;game=a; }
   // UI flags are sampled from the actual component passed by the original hook.
   ReadUnlocks(source);
   float now=Time.realtimeSinceStartup;if(sampledAt>=0&&now-sampledAt<0.25f)return;sampledAt=now;
   Type client=game.GetType("ClientData");
   object quest=StaticField(client,"m_SelectedQuest");
   int questId=Int(Field(quest,"id")), eventId=Int(Field(quest,"sp_event_id"));
   Type player=game.GetType("PlayerData");
   MethodInfo getter=player==null?null:player.GetMethod("GetEventPoint",Static,null,new Type[0],null);
   int point=getter==null?-1:Int(getter.Invoke(null,null));
   // Aggregation retains every requested per-enemy field and its multiplicity.
   Dictionary<string,int> groups=new Dictionary<string,int>();int count=0,present=0,total=0,targets=0;
   IEnumerable enemies=StaticField(client,"m_LastGetEnemyInfo") as IEnumerable;
   if(enemies!=null)foreach(object enemy in enemies) {
    if(enemy==null)continue;
    int eid=Int(Field(enemy,"enemy_id")),sid=Int(Field(enemy,"enemy_set_id")),p=Int(Field(enemy,"sp_event_point"));
    string flag=Bool(Field(enemy,"flag"));
    string key="\"enemy_id\":"+eid+",\"enemy_set_id\":"+sid+",\"sp_event_point\":"+p+",\"flag\":"+flag;
    int old;groups.TryGetValue(key,out old);groups[key]=old+1;count++;
    if(flag=="true"){present++;if(p>0)total+=p;if(sid==41028)targets++;}
   }
   List<string> keys=new List<string>(groups.Keys);keys.Sort();StringBuilder enemyJson=new StringBuilder("[");
   for(int i=0;i<keys.Count;i++){if(i>0)enemyJson.Append(',');enemyJson.Append('{').Append(keys[i]).Append(",\"count\":").Append(groups[keys[i]]).Append('}');}enemyJson.Append(']');
   object login=StaticField(client,"m_PlayerLogin");
   IEnumerable balances=Field(login,"sp_event_point_list") as IEnumerable;
   // null means not logged in; absent event after login means the original zero.
   string balance=login==null?"null":"0";
   if(balances!=null)foreach(object row in balances)if(Int(Field(row,"sp_event_id"))==100001){balance=Int(Field(row,"point")).ToString();break;}
   string value="{\"quest_id\":"+questId+",\"sp_event_id\":"+eventId+",\"run_points\":"+point+",\"event_100001_balance\":"+balance+",\"enemy_count\":"+count+",\"present_count\":"+present+",\"target_41028_count\":"+targets+",\"offered_points\":"+total+",\"enemy_groups\":"+enemyJson.ToString()+",\"quest_unlocks\":"+unlocks+"}";
   if(value!=previous){previous=value;Debug.Log("RTD_POINT_PROBE "+value);}
  } catch(Exception e) {
   // Type only: never print reflected values, account/session data, or messages.
   string kind=e.GetType().Name;if(lastError!=kind){lastError=kind;try{Debug.Log("RTD_POINT_PROBE {\"error\":\""+kind+"\"}");}catch(Exception){}}
  }
 }
}
