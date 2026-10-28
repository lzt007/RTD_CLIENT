// Compile against the client's own mscorlib and UnityEngine. Observation only.
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;

// Unity's shipped stripped mscorlib omits compiler-only attributes.
namespace System.Runtime.CompilerServices {
 [AttributeUsage(AttributeTargets.Assembly)]
 public sealed class CompilationRelaxationsAttribute : Attribute {
  public CompilationRelaxationsAttribute(int value) {}
 }
 [AttributeUsage(AttributeTargets.Assembly)]
 public sealed class RuntimeCompatibilityAttribute : Attribute {
  public bool WrapNonExceptionThrows {get;set;}
 }
}

public static class RTDFlow {
 static Dictionary<int,float> last = new Dictionary<int,float>();
 static bool loginProbe;
 static int observationSequence;
 static void LogObservation(string value) {
  if(value.Length<=250){Debug.Log("RTD_FLOW "+value);return;}
  List<string> parts=new List<string>();
  for(int start=0;start<value.Length;) {
   int end=Math.Min(start+150,value.Length);
   if(end<value.Length&&value[end-1]>='\ud800'&&value[end-1]<='\udbff')end--;
   parts.Add(value.Substring(start,end-start));start=end;
  }
  int seq=++observationSequence;
  for(int i=0;i<parts.Count;i++)Debug.Log("RTD_FLOW_CHUNK {\"seq\":"+seq+",\"index\":"+i+",\"total\":"+parts.Count+",\"data\":"+Quote(parts[i])+"}");
 }
 static object Field(object obj,string name) {
  if(obj==null)return null;
  for(Type type=obj.GetType();type!=null;type=type.BaseType) {
   FieldInfo f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
   if(f!=null)return f.GetValue(obj);
  }
  return null;
 }
 static int Number(object obj) {return obj==null?-1:Convert.ToInt32(obj);}
 static string AttackEstimates(Assembly game,Array members,object enemy,object battle) {
  if(members==null||enemy==null||Number(Field(enemy,"Life"))<=0)return "[]";
  try {
   Type defines=game.GetType("GlobalDefines"),passive=game.GetType("PassiveSkillManager"),party=game.GetType("Party");
   BindingFlags sf=BindingFlags.Public|BindingFlags.Static;
   float[] scales=(float[])defines.GetField("BattleAttackCountScale",sf).GetValue(null);
   int[,] totals=new int[6,8];
   foreach(object member in members) {
    if(member==null)continue;
    object data=Field(member,"CharaData");if(data==null)return "[]";
    // Advanced extra-class/compound attacks need separate original-count analysis.
    if(Convert.ToBoolean(Field(data,"AllPanelAttack"))||Number(Field(data,"ExPanelAttack"))!=0||Number(Field(data,"MultiAttackNum"))>0)return "[]";
    if(Number(Field(data,"JumpAttackTurn"))>=0)continue;
    object main=data.GetType().GetMethod("GetMainAttribute").Invoke(data,new object[]{true});
    object sub=data.GetType().GetMethod("GetSubAttribute").Invoke(data,new object[]{true});
    MethodInfo convert=game.GetType("ConvertClientToServer").GetMethod("ConvertAttribute");
    int mainAttr=Number(convert.Invoke(null,new object[]{main})),subAttr=Number(convert.Invoke(null,new object[]{sub}));
    int attack=Number(Property(data,"Attack"))+Number(member.GetType().GetMethod("GetAttackUpNum").Invoke(member,null));
    int killer=0;
    foreach(string name in new string[]{"GetDragonKillerNumber","GetDeathKillerNumber","GetUnitEnemyKillerNumber"})killer+=Number(passive.GetMethod(name).Invoke(null,null));
    int attribute=Number(Field(data,"AttributeCnt"))*(Number(defines.GetField("MoveAttributeScale",sf).GetValue(null))+
     Number(passive.GetMethod("GetAttributePanelNum").Invoke(null,new object[]{main,sub})));
    bool weak=Convert.ToBoolean(Field(data,"EnemyScan"))||RTDBattleEstimate.Weakness(mainAttr,subAttr,Number(Field(enemy,"Attribute")));
    int weakness=weak?(int)(Mathf.Pow(attack,(float)defines.GetField("BattleWeeknessScale3",sf).GetValue(null))*
     ((float)defines.GetField("BattleWeeknessScale4",sf).GetValue(null)+(float)party.GetField("AttackWeaknessUp",sf).GetValue(null))):0;
    object actClass=Property(data,"ActClass");int kind=Number(actClass);
    if(kind<0||kind>3)return "[]";
    float chain=Number(passive.GetMethod("GetChainSupportNum").Invoke(null,new object[]{actClass}))*.01f;
    int defence=Math.Max(Number(Field(enemy,"Defence"))+Number(Field(enemy,"DefenceEnhance")),0);
    for(int n=1;n<=8;n++) {
     int damage=RTDBattleEstimate.Damage(attack,attribute,killer,defence,weakness,scales,chain,n);
     totals[kind,n-1]+=damage;totals[5,n-1]+=damage;
    }
   }
   string result="[";
   for(int k=0;k<6;k++) {
    result+=(k==0?"":",")+"[";
    for(int n=0;n<8;n++)result+=(n==0?"":",")+totals[k,n];
    result+="]";
   }
   return result+"]";
  }catch(Exception){return "[]";}
 }
 static int Count(object obj) {ICollection items=obj as ICollection;return items==null?0:items.Count;}
 static object Property(object obj,string name) {
  if(obj==null)return null;
  PropertyInfo p=obj.GetType().GetProperty(name);return p==null?Field(obj,name):p.GetValue(obj,null);
 }
 static string Quote(string str) {return "\""+(str??"").Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";}
 static string Text(object obj) {
  if(obj==null)return "";
  PropertyInfo property=obj.GetType().GetProperty("text");
  return property==null?"":Convert.ToString(property.GetValue(obj,null));
 }
 static string Point(object obj,bool colliderCenter=false,bool visibleOnly=false) {
  Component c=obj as Component;if(c==null)return "null";
  Camera[] cameras=(Camera[])UnityEngine.Object.FindObjectsOfType(typeof(Camera));
  foreach(Camera camera in cameras) {
   if(!camera.enabled || (camera.cullingMask & (1<<c.gameObject.layer))==0)continue;
   Vector3 world=colliderCenter&&c.collider!=null?c.collider.bounds.center:c.transform.position;
   Vector3 point=camera.WorldToScreenPoint(world);
   if(visibleOnly&&(point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height))return "null";
   return "["+Math.Round(point.x)+","+Math.Round(Screen.height-point.y)+"]";
  }
  return "null";
 }
 static string OkPoint(object source,object dialog) {
  Component root=dialog as Component;if(root==null)return "null";
  Type buttonType=source.GetType().Assembly.GetType("UIButtonMessage");
  if(buttonType==null)return "null";
  foreach(Component button in root.GetComponentsInChildren<Component>()) {
   if(buttonType.IsInstanceOfType(button)&&Convert.ToString(Field(button,"functionName"))=="ClickOK")return Point(button);
  }
  return "null";
 }
 public static void Tick(object source) {
  try {
   if(source.GetType().Name=="GameManager") {
    if(Application.loadedLevel!=4)return;
    Type td=source.GetType().Assembly.GetType("TutorialDialog");
    foreach(UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(td))
     if((bool)Field(candidate,"m_isOpen")){source=candidate;break;}
   }
   Component component=source as Component;if(component==null)return;
   int id=component.GetInstanceID();float previous;
   if(last.TryGetValue(id,out previous)&&Time.realtimeSinceStartup-previous<0.5f)return;
   last[id]=Time.realtimeSinceStartup;
   bool tutorial=source.GetType().Name=="TutorialDialog";
   object dialog=tutorial?source:Field(source,"m_Dialog");
   if(dialog==null)dialog=Field(source,"m_dialog");
   if(source.GetType().Name=="MMUnitSell"&&Number(Field(source,"m_Mode"))==600)
    dialog=Field(source,"m_ApRecDialog");
   if(source.GetType().Name=="MMFriendSearch") {
    int friendMode=Number(Field(source,"m_Mode"));
    if(friendMode==2)dialog=Field(source,"m_InputDialog");
    if(friendMode==100)dialog=Field(source,"m_ResultDialog");
   }
   if(source.GetType().Name=="MMEtcChangeName"&&Number(Field(source,"m_Mode"))==2)
    dialog=Field(source,"m_NameInputDialog");
   Array labels=Field(dialog,"ButtonLabels") as Array;
   Array messages=Field(dialog,"Messages") as Array;
   string text=messages!=null&&messages.Length>0?Text(messages.GetValue(0)):"";
   if(tutorial)text=Text(Field(source,"Message"));
   object input=Field(source,"m_InputNameStr");
   Behaviour selection=Field(source,"m_SelectButton") as Behaviour;
   bool selectionEnabled=selection!=null&&selection.enabled;
   Assembly game=source.GetType().Assembly;
   Type clientType=game.GetType("ClientData");
   object player=clientType.GetField("m_ComPlayerData",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   int userId=Number(Field(player,"user_id"));
   Type comType=game.GetType("ComManager");
   bool seqEnd=(bool)comType.GetField("m_SeqEnd",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   if(loginProbe&&seqEnd) {
    Debug.Log("RTD_LOGIN_PROBE completed "+comType.GetField("m_LastError",BindingFlags.Public|BindingFlags.Static).GetValue(null));
    loginProbe=false;
   }
   if(source.GetType().Name=="TitleMenu"&&Number(Field(source,"m_Mode"))==203&&userId>0&&seqEnd&&File.Exists("/sdcard/rtd-login-probe")) {
    File.Delete("/sdcard/rtd-login-probe");
    MonoBehaviour com=UnityEngine.Object.FindObjectOfType(comType) as MonoBehaviour;
    if(com!=null) {
     com.StartCoroutine((IEnumerator)comType.GetMethod("Login").Invoke(com,null));
     loginProbe=true;Debug.Log("RTD_LOGIN_PROBE started");
    }
   }
   string points="[";
   Array chars=Field(source,"m_DefaultCharaTrans") as Array;
   if(chars!=null)for(int n=0;n<chars.Length;n++)points+=(n>0?",":"")+Point(chars.GetValue(n));
   points+="]";
   string controls="[]", panelDetails="[]", fieldState="{}", gameState="", buttons="[";bool unlocked=false;
   int buttonCount=0;
   foreach(Component button in component.GetComponentsInChildren<Component>()) {
    if(button.GetType().Name!="UIButtonMessage"||button.collider==null||!button.collider.enabled)continue;
    if(buttonCount++>=3)break;
    if(buttons!="[")buttons+=",";
    buttons+="{\"action\":"+Quote(Convert.ToString(Field(button,"functionName")))+",\"point\":"+Point(button)+"}";
   }
   buttons+="]";
   string shopActions="[";
   if(source.GetType().Name=="MMShop") {
    List<string> seenShopButtons=new List<string>();
    foreach(Component button in component.GetComponentsInChildren<Component>()) {
     if(button.collider==null||!button.collider.enabled||!button.gameObject.active||seenShopButtons.Contains(button.name))continue;
     if(button.name!="Button_2_stamina"&&button.name!="Button_3_slotplus"&&button.name!="Button_4_friendplus")continue;
     seenShopButtons.Add(button.name);
     shopActions+=(shopActions!="["?",":"")+"{\"name\":"+Quote(button.name)+",\"point\":"+Point(button,true)+"}";
    }
   }
   shopActions+="]";
   string profileActions="[";
   if(source.GetType().Name=="MMEtcMenu") {
    foreach(Component button in component.GetComponentsInChildren<Component>()) {
     if(button.name!="Button_05_change_name"||button.collider==null||!button.collider.enabled||!button.gameObject.active)continue;
     profileActions+=(profileActions!="["?",":"")+"{\"name\":"+Quote(button.name)+",\"point\":"+Point(button,true)+"}";
    }
   }
   profileActions+="]";
   string friendActions="[",friendRows="[",helperRows="[";
   if(source.GetType().Name=="MMFriendMenu") {
    List<string> seen=new List<string>();
    foreach(Component button in component.GetComponentsInChildren<Component>()) {
     if(button.collider==null||!button.collider.enabled||!button.gameObject.active||seen.Contains(button.name))continue;
     if(button.name!="Button_0_list"&&button.name!="Button_1_post"&&button.name!="Button_2_apply"&&button.name!="Button_3_search")continue;
     seen.Add(button.name);friendActions+=(friendActions!="["?",":"")+"{\"name\":"+Quote(button.name)+",\"point\":"+Point(button,true)+"}";
    }
   }
   friendActions+="]";
   if(source.GetType().Name.StartsWith("MMFriend")) {
    foreach(Component panel in component.GetComponentsInChildren<Component>()) {
     if(panel.GetType().Name!="FriendPanel")continue;
     object data=Field(panel,"friendData");if(data==null||!panel.gameObject.active)continue;
     string rowButtons="[";
     foreach(Collider button in panel.transform.parent.GetComponentsInChildren<Collider>()) {
      if(!button.enabled||!button.gameObject.active||(button.name!="Button_01"&&button.name!="Button_02"))continue;
      rowButtons+=(rowButtons!="["?",":"")+"{\"name\":"+Quote(button.name)+",\"point\":"+Point(button,true)+"}";
     }
     friendRows+=(friendRows!="["?",":"")+"{\"userId\":"+Number(Field(data,"user_id"))+",\"status\":"+Number(Field(data,"friend_status"))+",\"buttons\":"+rowButtons+"]}";
    }
   }
   friendRows+="]";
   if(source.GetType().Name=="MMHelper") {
    IEnumerable rows=Field(source,"m_List") as IEnumerable;
    if(rows!=null)foreach(object row in rows) {
     Component root=Field(row,"m_Transform") as Component;object data=Field(row,"m_HelperData");
     if(root==null||data==null)continue;
     foreach(Collider button in root.GetComponentsInChildren<Collider>()) {
      if(button.name!="A"||!button.enabled)continue;
      helperRows+=(helperRows!="["?",":"")+"{\"userId\":"+Number(Field(data,"user_id"))+",\"point\":"+Point(button,true)+"}";break;
     }
    }
   }
   helperRows+="]";
   int turn=-1,progress=-1;
   Component status=UnityEngine.Object.FindObjectOfType(game.GetType("StatusManager")) as Component;
   gameState=Convert.ToString(Property(status,"CurrentGameState"));
   object restore=game.GetType("SaveDataManager").GetField("m_GameRestore",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   object restoreHeader=game.GetType("SaveDataManager").GetField("m_GameRestoreHeader",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   object serverBattle=game.GetType("ComManager").GetField("m_ServerBattleInfo",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   Component gameStatus=UnityEngine.Object.FindObjectOfType(game.GetType("GameStatus")) as Component;
   string continueButtons="[";
   GameObject continuePanel=Field(gameStatus,"PanelContinue") as GameObject;
   if(continuePanel!=null && (bool)Field(gameStatus,"m_ViewContinueDialog"))
    foreach(Component button in continuePanel.GetComponentsInChildren<Component>()) {
     if(button.GetType().Name!="UIButtonMessage")continue;
     string action=Convert.ToString(Field(button,"functionName"));
     if(action!="ContinueYes" && action!="ContinueNo")continue;
     continueButtons+=(continueButtons!="["?",":"")+"{\"action\":"+Quote(action)+",\"point\":"+Point(button)+"}";
    }
   continueButtons+="]";
   Component tuto=UnityEngine.Object.FindObjectOfType(game.GetType("TutorialManager")) as Component;
   progress=Number(Field(tuto,"StateOfProgress"));
   if(source.GetType().Name=="GameManager") {
    Type panelType=game.GetType("SelectPanel");
    Component manager=UnityEngine.Object.FindObjectOfType(game.GetType("SelectPanelManager")) as Component;
    if(manager!=null)unlocked=!(bool)Field(manager,"systemlock")&&(bool)manager.GetType().GetMethod("CanSelect").Invoke(manager,null);
    turn=Number(Property(manager,"TurnCount"));
    Component fieldManager=UnityEngine.Object.FindObjectOfType(game.GetType("LevelPanelManager")) as Component;
    Component death=Field(fieldManager,"DeathEventSprite") as Component;
    Component objects=UnityEngine.Object.FindObjectOfType(game.GetType("FieldObjectManager")) as Component;
    Component discovery=Field(objects,"DiscoverDialog") as Component;
    Component cells=UnityEngine.Object.FindObjectOfType(game.GetType("LevelCellManager")) as Component;
    Type partyType=game.GetType("Party");
    string partyClasses="[";
    Array members=partyType.GetField("PartyUnit",BindingFlags.Public|BindingFlags.Static).GetValue(null) as Array;
    if(members!=null)foreach(object member in members) {
     object data=Field(member,"CharaData");if(data==null)continue;
     partyClasses+=(partyClasses!="["?",":"")+Number(Property(data,"ActClass"));
    }
    partyClasses+="]";
    // Read original skill availability/effects only. Execution remains UI input.
    Component skillManager=UnityEngine.Object.FindObjectOfType(game.GetType("ActiveSkillManager")) as Component;
    Component skillDialog=null;
    GameObject skillRoot=Field(skillManager,"Dialog") as GameObject;
    if(skillRoot!=null)skillDialog=skillRoot.GetComponent(game.GetType("UISkillDialog"));
    Component battleManager=UnityEngine.Object.FindObjectOfType(game.GetType("BattleManager")) as Component;
    object enemy=Field(battleManager,"Enemy");
    bool enemyBoss=enemy!=null&&Convert.ToBoolean(Property(enemy,"IsBoss"));
    string combatSkills="[";
    if(members!=null&&Convert.ToBoolean(partyType.GetField("InitializeFlg",BindingFlags.Public|BindingFlags.Static).GetValue(null)))
     foreach(Component button in UnityEngine.Object.FindObjectsOfType(game.GetType("UIButtonMessage"))) {
      string action=Convert.ToString(Field(button,"functionName"));int slot;
      if(!action.StartsWith("CreateSkillDialog")||!Int32.TryParse(action.Substring(17),out slot))continue;
      slot--;if(slot<0||slot>=members.Length||members.GetValue(slot)==null)continue;
      if(!button.gameObject.active||button.collider==null||!button.collider.enabled)continue;
      object data=Field(members.GetValue(slot),"CharaData"),skill=Field(data,"m_AsMaster");
      if(skill==null)continue;
      int type=Number(Field(skill,"type")),num=Number(Field(skill,"num")),attr=Number(Field(skill,"attribute"));
      bool applied=false,useful=type==12||type==13;
      if(type==8) {
       int defence=Number(Field(enemy,"Defence")),enhancement=Number(Field(enemy,"DefenceEnhance"));
       applied=enhancement<0;useful=defence>=1000&&enhancement==0;
      }
      if(type==12)applied=(float)partyType.GetField("AttackWeaknessUp",BindingFlags.Public|BindingFlags.Static).GetValue(null)>=num;
      if(type==13)applied=Number(Field(data,"AttackUp"))>=num;
      if(type==36) {
       Array effects=Field(skillManager,"m_ColorEnhance") as Array;
       applied=effects!=null&&attr>=0&&attr<effects.Length&&Number(effects.GetValue(attr))>=num;
       foreach(object member in members)if(member!=null) {
        object memberData=Field(member,"CharaData");
        if(memberData!=null&&RTDSkillAttributes.Matches(game,memberData,attr))useful=true;
       }
      }
      bool available=unlocked&&skillManager!=null&&skillRoot!=null&&
       (bool)skillManager.GetType().GetMethod("CheckSkill").Invoke(skillManager,new object[]{slot,skill});
      combatSkills+=(combatSkills!="["?",":"")+"{\"partyIndex\":"+slot+",\"id\":"+Number(Field(skill,"id"))+
       ",\"type\":"+type+",\"available\":"+(available?"true":"false")+",\"applied\":"+(applied?"true":"false")+
       ",\"useful\":"+(useful?"true":"false")+",\"point\":"+Point(button,true,true)+"}";
     }
    combatSkills+="]";
    string skillObservation="{\"active\":false}";
    if(skillDialog!=null&&skillRoot.active) {
     object skill=Field(Field(skillDialog,"cData"),"m_AsMaster");int cost;
     if(!Int32.TryParse(Text(Field(skillDialog,"ActiveSkillCost")),out cost))cost=-1;
     string skillButtons="[";
     foreach(Component button in skillDialog.GetComponentsInChildren<Component>()) {
      if(button.GetType().Name!="UIButtonMessage"||!button.gameObject.active||button.collider==null||!button.collider.enabled)continue;
      string action=Convert.ToString(Field(button,"functionName"));if(action!="ExecSkill"&&action!="ExitDialog")continue;
      skillButtons+=(skillButtons!="["?",":"")+"{\"action\":"+Quote(action)+",\"point\":"+Point(button,true,true)+"}";
     }
     skillButtons+="]";
     skillObservation="{\"active\":true,\"status\":"+Quote(Convert.ToString(Field(skillDialog,"m_Status")))+
      ",\"partyIndex\":"+Number(Field(skillDialog,"PartyNumber"))+",\"id\":"+Number(Field(skill,"id"))+
      ",\"type\":"+Number(Field(skill,"type"))+",\"cost\":"+cost+",\"buttons\":"+skillButtons+"}";
    }
    string discoverButtons="[";
    if(discovery!=null&&discovery.gameObject.active&&discovery.animation!=null&&!discovery.animation.isPlaying)
     foreach(Component button in discovery.GetComponentsInChildren<Component>()) {
      if(button.GetType().Name!="UIButtonMessage")continue;
      string action=Convert.ToString(Field(button,"functionName"));
      if(action!="ButtonYES"&&action!="ButtonNO")continue;
      if(button.collider==null||!button.collider.enabled)continue;
      discoverButtons+=(discoverButtons!="["?",":"")+"{\"action\":"+Quote(action)+",\"point\":"+Point(button,true)+"}";
     }
    discoverButtons+="]";
    fieldState="{\"enabled\":"+Quote(Convert.ToString(Property(fieldManager,"enabled")))+
     ",\"active\":"+(fieldManager!=null&&fieldManager.gameObject.active?"true":"false")+
     ",\"deltaTime\":"+Quote(Convert.ToString(Time.deltaTime))+
     ",\"partyInitialized\":"+Quote(Convert.ToString(game.GetType("Party").GetField("InitializeFlg",BindingFlags.Public|BindingFlags.Static).GetValue(null)))+
     ",\"partySoul\":"+Number(partyType.GetField("Soul",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
     ",\"partyLife\":"+Number(partyType.GetField("Life",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
     ",\"partyMaxLife\":"+Number(partyType.GetField("MaxLife",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
     ",\"partyClasses\":"+partyClasses+
     ",\"attackEstimates\":"+(unlocked?AttackEstimates(game,members,enemy,battleManager):"[]")+
     ",\"enemyMasterId\":"+Number(Field(Field(enemy,"m_EnemyData"),"ID"))+
     ",\"enemyLife\":"+Number(Field(enemy,"Life"))+",\"enemyMaxLife\":"+Number(Field(enemy,"MaxLife"))+
     ",\"enemyDefence\":"+Number(Field(enemy,"Defence"))+",\"enemyDefenceEnhance\":"+Number(Field(enemy,"DefenceEnhance"))+
     ",\"enemyBoss\":"+(enemyBoss?"true":"false")+",\"combatSkills\":"+combatSkills+",\"skillDialog\":"+skillObservation+
     ",\"systemLock\":"+Quote(Convert.ToString(Field(manager,"systemlock")))+
     ",\"cellX\":"+Number(Property(cells,"CurrentX"))+
     ",\"cellY\":"+Number(Property(cells,"CurrentY"))+
     ",\"step\":"+Number(Field(fieldManager,"QuestStepCount"))+
     ",\"nextQuestId\":"+Number(Field(Field(objects,"NextQuest"),"id"))+
     ",\"discoverButtons\":"+discoverButtons+
     ",\"canSelect\":"+Quote(Convert.ToString(Field(manager,"canSelect")))+
     ",\"scrollTime\":"+Quote(Convert.ToString(Field(fieldManager,"scrollTimeRest")))+
     ",\"currentPanel\":"+(Field(fieldManager,"CurrentPanel")==null?"false":"true")+
     ",\"deathFlag\":"+Quote(Convert.ToString(Field(fieldManager,"DeathFlg")))+
     ",\"deathActive\":"+(death!=null&&death.gameObject.active?"true":"false")+
     ",\"deathAnimating\":"+(death!=null&&death.animation!=null&&death.animation.isPlaying?"true":"false")+"}";
    controls="[";panelDetails="[";bool comma=false;
    foreach(Component panel in UnityEngine.Object.FindObjectsOfType(panelType)) {
     object data=Field(panel,"PanelData");
     if(!unlocked||data==null||(bool)Field(data,"Lock")||panel.collider==null||!panel.collider.enabled)continue;
     controls+=(comma?",":"")+Point(panel);
     panelDetails+=(comma?",":"")+"{\"point\":"+Point(panel)+",\"way\":"+Number(Field(Field(data,"Way"),"WayBit"))+",\"class\":"+Number(Field(data,"CharacterClass"))+"}";comma=true;
    }
    controls+="]";panelDetails+="]";
   }
   Component loader=UnityEngine.Object.FindObjectOfType(game.GetType("LevelLoader")) as Component;
   object map=Field(loader,"Record");
   object quest=clientType.GetField("m_SelectedQuest",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   object selectedQuest=game.GetType("MainMenu").GetField("m_SelectedQuest",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   string questRows="[";
   if(source.GetType().Name=="MainMenu"&&Convert.ToString(Field(source,"m_MenuMode"))=="SELECT_QUEST") {
    Component questMenu=UnityEngine.Object.FindObjectOfType(game.GetType("MMQuest")) as Component;
    IEnumerable rows=Field(questMenu,"m_List") as IEnumerable;
    int count=0;
    if(rows!=null)foreach(object row in rows) {
     Component root=Field(row,"m_Transform") as Component;
     // MMQuest handles collider hit names, not UIButtonMessage callbacks.
     Component button=root;
     if(button==null||button.collider==null||!button.collider.enabled)continue;
     if(count++>=4)break;
     questRows+=(questRows!="["?",":"")+"{\"id\":"+Number(Field(Field(row,"m_Item"),"id"))+",\"point\":"+Point(button,true)+"}";
    }
   }
   questRows+="]";
   string areaRows="[",areaKindButton="null";
   string areaMode=Convert.ToString(Field(source,"m_MenuMode"));
   if(source.GetType().Name=="MainMenu"&&areaMode.StartsWith("SELECT_AREA")) {
    string areaType=areaMode=="SELECT_AREA_EVENT"?"MMAreaEvent":areaMode=="SELECT_AREA_SPECIAL"?"MMAreaSpecial":"MMArea";
    Component areaMenu=UnityEngine.Object.FindObjectOfType(game.GetType(areaType)) as Component;
    IEnumerable rows=Field(areaMenu,"m_List") as IEnumerable;
    if(rows!=null)foreach(object row in rows) {
     Component root=Field(row,"m_Transform") as Component;object item=Field(row,"m_QuestMaster");
     if(root==null||item==null||!root.gameObject.active)continue;
     string point=root.collider!=null&&root.collider.enabled?Point(root,true,true):"null";
     areaRows+=(areaRows!="["?",":"")+"{\"category\":"+Number(Field(item,"category"))+
      ",\"division\":"+Number(Field(item,"division"))+",\"point\":"+point+
      ",\"scrollPoint\":"+Point(root,true,false)+"}";
    }
    if(areaMenu!=null)foreach(Component button in areaMenu.GetComponentsInChildren<Component>()) {
     if(button.GetType().Name!="UIButtonMessage"||!button.gameObject.active||button.collider==null||!button.collider.enabled)continue;
     if(Convert.ToString(Field(button,"functionName"))=="ChangeAreaKind") {areaKindButton=Point(button,true,true);break;}
    }
   }
   areaRows+="]";
   object partyInfo=clientType.GetField("m_PartyInfo",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   string partyGroups="[";
   Array groups=Field(partyInfo,"party") as Array;
   if(groups!=null)foreach(object group in groups) {
    if(partyGroups!="[")partyGroups+=",";
    partyGroups+="[";bool comma=false;
    Array instances=Field(group,"instanceIds") as Array;
    if(instances!=null)foreach(object instance in instances){partyGroups+=(comma?",":"")+Convert.ToString(instance);comma=true;}
    partyGroups+="]";
   }
   partyGroups+="]";
   string unitRows="[";
   if(source.GetType().Name=="MainMenu"||source.GetType().Name.StartsWith("MMUnit")||source.GetType().Name=="MMFriendListSelPresent") {
    int count=0;
    foreach(Component panel in UnityEngine.Object.FindObjectsOfType(game.GetType("UnitPanel"))) {
     object unit=Field(panel,"unit");
     if(unit==null||panel.collider==null||!panel.collider.enabled)continue;
     string unitPoint=Point(panel,true,true);
     if(unitPoint=="null")continue;
     if(count++>=24)break;
     unitRows+=(unitRows!="["?",":"")+"{\"instanceId\":"+Convert.ToString(Field(unit,"instance_id"))+",\"unitId\":"+Number(Field(unit,"unit_id"))+
      ",\"favorite\":"+Number(Field(unit,"favorite"))+",\"point\":"+unitPoint+"}";
    }
   }
   unitRows+="]";
   string composePageNext="null";
   if(source.GetType().Name=="MMUnitUnionBase"||source.GetType().Name=="MMUnitUnionList") {
    // MMUnitList owns the arrow; its original callback forwards to this scene.
    Component list=Field(source,"m_UnitList") as Component;
    if(list!=null&&(Field(list,"m_TouchCallbackObj") as GameObject)==component.gameObject&&
       Convert.ToString(Field(list,"m_nextMessage"))=="ClickNextPage")
    foreach(Component button in list.GetComponentsInChildren<Component>()) {
     if(button.GetType().Name!="UIButtonMessage"||button.collider==null||!button.collider.enabled||
        (Field(button,"target") as GameObject)!=list.gameObject)continue;
     if(Convert.ToString(Field(button,"functionName"))=="ClickPageNext")composePageNext=Point(button,true,true);
    }
   }
   string saleSelected="[";int salePrice=0;
   Array saleUnits=Field(Field(source,"m_UnitSelector"),"units") as Array;
   if(source.GetType().Name=="MMUnitSell"&&saleUnits!=null)foreach(object unit in saleUnits) {
    if(unit==null)continue;
    saleSelected+=(saleSelected!="["?",":"")+Convert.ToString(Field(unit,"instance_id"));
    salePrice+=Convert.ToInt32(Field(Field(unit,"master"),"sale"))*Convert.ToInt32(Field(unit,"level"));
   }
   saleSelected+="]";
   ICollection ownedUnits=clientType.GetField("m_PlayerUnits",BindingFlags.Public|BindingFlags.Static).GetValue(null) as ICollection;
   Type unionType=game.GetType("MMUnitUnion");
   object composeBase=unionType.GetField("m_BaseUnit",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   string composeSelected="[";int composeCount=0,composeExp=0,composePrice=0;
   Array composeItems=null;
   if(source.GetType().Name=="MMUnitUnionList")composeItems=Field(Field(source,"m_UnitSelector"),"units") as Array;
   if(source.GetType().Name=="MMUnitUnion")composeItems=Field(source,"m_UnitPanels") as Array;
   if(composeItems!=null)foreach(object item in composeItems) {
    object unit=source.GetType().Name=="MMUnitUnion"?Field(item,"unit"):item;
    if(unit==null)continue;
    composeSelected+=(composeSelected!="["?",":"")+Convert.ToString(Field(unit,"instance_id"));
    composeCount++;composeExp+=Convert.ToInt32(unit.GetType().GetMethod("UnionPoint").Invoke(unit,null));
   }
   composeSelected+="]";
   if(composeBase!=null)composePrice=Convert.ToInt32(Field(composeBase,"level"))*Convert.ToInt32(Field(Field(composeBase,"master"),"cost"))*composeCount;
   object evoBase=game.GetType("MMUnitEvo").GetField("m_BaseUnit",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   string evoSelected="[";int evoPoints=0;
   Array evoItems=null;
   if(source.GetType().Name=="MMUnitEvoList")evoItems=Field(Field(source,"m_UnitSelector"),"units") as Array;
   if(source.GetType().Name=="MMUnitEvo")evoItems=Field(source,"m_UnitPanels") as Array;
   if(evoItems!=null)foreach(object item in evoItems) {
    object unit=source.GetType().Name=="MMUnitEvo"?Field(item,"unit"):item;
    if(unit==null)continue;
    evoSelected+=(evoSelected!="["?",":"")+Convert.ToString(Field(unit,"instance_id"));
    evoPoints+=Convert.ToInt32(unit.GetType().GetMethod("UnitEvoPoint").Invoke(unit,null));
   }
   evoSelected+="]";
   int evoRequired=evoBase==null?0:Convert.ToInt32(Field(Field(evoBase,"master"),"need_pt"));
   object specialBase=game.GetType("MMUnitSpComp").GetField("m_BaseUnit",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   object specialMaterial=game.GetType("MMUnitSpComp").GetField("m_DstUnit",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   int specialTotal=specialBase==null?0:Convert.ToInt32(specialBase.GetType().GetMethod("GetTotalBonus").Invoke(specialBase,null));
   object challenge=Field(source,"m_ChallengeMenu");
   object challengeReward=Field(challenge,"m_ChallengeRewardMaster");
   object selectedPresent=Field(Field(source,"m_Selected"),"present");
   string presentRows="[";
   IEnumerable presentList=source.GetType().Name=="MMEtcPresent"?Field(source,"m_List") as IEnumerable:null;
   int presentCount=0;
   if(presentList!=null)foreach(object item in presentList) {
    object gift=Field(item,"present");if(gift==null)continue;
    if(presentCount++>=20)break;
    presentRows+=(presentRows!="["?",":"")+"{\"serial\":"+Convert.ToString(Field(gift,"serial_number"))+
     ",\"type\":"+Number(Field(gift,"present_type"))+",\"param\":"+Number(Field(gift,"present_param_0"))+
     ",\"level\":"+Number(Field(gift,"present_param_1"))+",\"point\":"+Point(item,true)+"}";
   }
   presentRows+="]";
   Component mainMenu=UnityEngine.Object.FindObjectOfType(game.GetType("MainMenu")) as Component;
   Component unitDialog=Field(mainMenu,"m_DialogUnitMenu") as Component;
   object dialogUnit=unitDialog!=null&&unitDialog.gameObject.active?Field(Field(unitDialog,"m_UnitPanels"),"unit"):null;
   Component drops=UnityEngine.Object.FindObjectOfType(game.GetType("DropManager")) as Component;
   string dropOk="null";
   GameObject dropDialog=Field(drops,"Dialog") as GameObject;
   if(dropDialog!=null)foreach(Component c in dropDialog.GetComponentsInChildren<Component>())
    if(c.name=="ok"&&c.collider!=null&&c.collider.enabled){dropOk=Point(c);break;}
   object login=clientType.GetField("m_PlayerLogin",BindingFlags.Public|BindingFlags.Static).GetValue(null);
   object loginBonus=Field(login,"sequence_login_bonus");
   Component loginCalendar=Field(source,"m_UILoginBonusDialog") as Component;
   bool loginCalendarActive=loginCalendar!=null&&loginCalendar.gameObject.active&&!(bool)Field(loginCalendar,"m_IsEnd");
   string appliedBonus="[";
   foreach(string bonusField in new[]{"m_BonusLife","m_BonusAttack","m_BonusRecovery","m_BonusEvoPoint","m_BonusSoulPoint","m_BonusCriticalRate","m_BonusDropRate","m_BonusQuestMoney","m_BonusQuestExp","m_BonusQuestFriendPoint"})
    appliedBonus+=(appliedBonus!="["?",":"")+Number(clientType.GetField(bonusField,BindingFlags.Public|BindingFlags.Static).GetValue(null));
   appliedBonus+="]";
   object giftFriend=Field(Field(game.GetType("MMFriendList").GetField("Instance",BindingFlags.Public|BindingFlags.Static).GetValue(null),"m_SelectPanel"),"friendData");
   LogObservation("{\"scene\":"+Quote(source.GetType().Name)+
    ",\"restoreEnabled\":"+Number(Field(restoreHeader,"m_Enable"))+",\"restoreQuestId\":"+Number(Field(restore,"m_QuestID"))+
    ",\"restoreContinue\":"+Number(Field(restore,"m_ContinueCt"))+",\"restorePhase\":"+Number(Field(restore,"m_GamePhase"))+
    ",\"serverQuestId\":"+Number(Field(serverBattle,"quest_id"))+",\"serverContinue\":"+Number(Field(serverBattle,"quest_continue"))+
    ",\"continuePanelOpen\":"+(gameStatus!=null && (bool)Field(gameStatus,"m_ViewContinueDialog")?"true":"false")+",\"continueButtons\":"+continueButtons+
    ",\"mode\":"+Number(Field(source,"m_Mode"))+",\"userId\":"+userId+
    ",\"loginDays\":"+Number(Field(login,"login_days"))+",\"loginStreak\":"+Number(Field(login,"sequence_login_days"))+
    ",\"helpCount\":"+Number(Field(login,"help_count"))+",\"receivedFriendPoint\":"+Number(Field(login,"received_friend_point"))+
    ",\"loginBonusType\":"+Number(Field(loginBonus,"type"))+",\"loginBonusNum\":"+Number(Field(loginBonus,"num"))+",\"appliedLoginBonus\":"+appliedBonus+
    ",\"loginCalendarActive\":"+(loginCalendarActive?"true":"false")+",\"loginCalendarOk\":"+OkPoint(source,loginCalendar)+
    ",\"dialog\":"+(tutorial?((bool)Field(source,"m_isOpen")?2:0):Number(Field(dialog,"dmode")))+
    ",\"button\":"+Number(Field(dialog,"button"))+
    ",\"choice\":"+Number(Field(source,"m_SelectIndex"))+
    ",\"selectionEnabled\":"+(selectionEnabled?"true":"false")+
    ",\"ok\":"+OkPoint(source,dialog)+
    ",\"input\":"+Point(input)+",\"nameLength\":"+Text(input).Length+
    ",\"playerName\":"+Quote(Convert.ToString(Field(player,"user_name")))+
    ",\"profileActions\":"+profileActions+",\"profileInput\":"+Point(Field(source,"m_Input"))+
    ",\"profileText\":"+Quote(Text(Field(source,"m_Input")))+
    ",\"questId\":"+Number(Field(quest,"id"))+",\"mapId\":"+Number(Field(map,"level_data_id"))+
    ",\"selectedQuestId\":"+Number(Field(selectedQuest,"id"))+",\"questRows\":"+questRows+
    ",\"areaRows\":"+areaRows+",\"areaKindButton\":"+areaKindButton+
    ",\"activeParty\":"+Number(Field(partyInfo,"active"))+",\"partyGroups\":"+partyGroups+",\"unitRows\":"+unitRows+
    ",\"composePage\":"+Number(Field(source,"m_NowPage"))+",\"composePageNext\":"+composePageNext+
    ",\"unitDialogInstanceId\":"+(dialogUnit==null?"0":Convert.ToString(Field(dialogUnit,"instance_id")))+
    ",\"challengeRewardId\":"+Number(Field(challengeReward,"id"))+",\"challengeCategory\":"+Number(Field(challenge,"m_AreaCategory"))+
    ",\"challengeCount\":"+Number(Field(challenge,"m_ClearTotalNumber"))+",\"presentRows\":"+presentRows+
    ",\"selectedPresentSerial\":"+(selectedPresent==null?"0":Convert.ToString(Field(selectedPresent,"serial_number")))+
    ",\"unitCount\":"+(ownedUnits==null?-1:ownedUnits.Count)+",\"saleSelected\":"+saleSelected+",\"salePrice\":"+salePrice+
    ",\"composeBaseId\":"+(composeBase==null?"0":Convert.ToString(Field(composeBase,"instance_id")))+
    ",\"composeBaseLevel\":"+Number(Field(composeBase,"level"))+",\"composeBaseExp\":"+Number(Field(composeBase,"exp"))+
    ",\"composeSelected\":"+composeSelected+",\"composePrice\":"+composePrice+",\"composePreviewExp\":"+composeExp+
    ",\"evoBaseId\":"+(evoBase==null?"0":Convert.ToString(Field(evoBase,"instance_id")))+
    ",\"evoBaseUnitId\":"+Number(Field(evoBase,"unit_id"))+",\"evoBaseLevel\":"+Number(Field(evoBase,"level"))+
    ",\"evoSelected\":"+evoSelected+",\"evoPoints\":"+evoPoints+",\"evoRequired\":"+evoRequired+",\"evoPrice\":"+(evoRequired*100)+
    ",\"evoAttribute\":"+Number(game.GetType("MMUnitEvo").GetField("m_EvoAttribute",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
    ",\"specialBaseId\":"+(specialBase==null?"0":Convert.ToString(Field(specialBase,"instance_id")))+
    ",\"specialMaterialId\":"+(specialMaterial==null?"0":Convert.ToString(Field(specialMaterial,"instance_id")))+
    ",\"specialTotal\":"+specialTotal+",\"specialPrice\":"+((specialTotal+1)*25000)+
    ",\"mapHash\":"+Quote(Convert.ToString(Field(map,"hash")))+
    ",\"rank\":"+Number(Field(player,"rank"))+",\"exp\":"+Number(Field(player,"exp"))+
    ",\"ap\":"+Number(Field(player,"action_point"))+",\"maxAp\":"+Number(Field(player,"max_action_point"))+
    ",\"apRecover\":"+Number(Field(player,"action_point_recover"))+
    ",\"money\":"+Number(Field(player,"game_money"))+
    ",\"maxUnits\":"+Number(Field(player,"max_unit"))+",\"maxFriends\":"+Number(Field(player,"max_friend"))+
    ",\"shopActions\":"+shopActions+
    ",\"friendActions\":"+friendActions+",\"friendRows\":"+friendRows+",\"helperRows\":"+helperRows+
    ",\"friendCount\":"+Count(clientType.GetField("m_Friends",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
    ",\"incomingFriends\":"+Count(clientType.GetField("m_FriendsPost",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
    ",\"outgoingFriends\":"+Count(clientType.GetField("m_FriendsApply",BindingFlags.Public|BindingFlags.Static).GetValue(null))+
    ",\"giftTargetId\":"+Number(Field(giftFriend,"user_id"))+
    ",\"giftSelectedInstance\":"+Number(Field(Field(Field(source,"m_SelectUnit"),"unit"),"instance_id"))+
    ",\"friendFindText\":"+Quote(Text(Field(source,"m_LabelFindID")))+",\"friendInputPoint\":"+Point(Field(source,"m_LabelFindID"))+
    ",\"selectedFriendId\":"+Number(Field(Field(Field(source,"m_SelectPanel"),"friendData"),"user_id"))+
    ",\"selectedHelperId\":"+Number(Field(game.GetType("MainMenu").GetField("m_HelperData",BindingFlags.Public|BindingFlags.Static).GetValue(null),"user_id"))+
    ",\"reviewPoint\":"+Point(Property(Field(source,"m_ReviewButton"),"transform"),true)+
    ",\"friendPoints\":"+Number(Field(player,"friend_point"))+
    ",\"gachaType\":"+Quote(Convert.ToString(Field(source,"m_GachaType")))+
    ",\"gachaResultCount\":"+(Field(source,"m_GetUnits") is Array?((Array)Field(source,"m_GetUnits")).Length:0)+
    ",\"gachaResultOk\":"+Point(Property(Field(Field(source,"m_Dialog_GetUnits"),"m_Button_OK"),"transform"),true)+
    ",\"crystals\":"+Number(Field(player,"total_paid_point"))+",\"freeCrystals\":"+Number(Field(player,"free_paid_point"))+",\"paidCrystals\":"+Number(Field(player,"paid_point"))+
    ",\"dropDialogState\":"+Number(Field(drops,"State"))+",\"dropBookCount\":"+Number(Field(drops,"BookCount"))+
    ",\"dropThrowIndex\":"+Number(Field(drops,"CurrentThrowBook"))+
    ",\"dropBookOk\":"+dropOk+
    ",\"characters\":"+points+",\"controlsUnlocked\":"+(unlocked?"true":"false")+
    ",\"panels\":"+controls+",\"panelDetails\":"+panelDetails+",\"fieldState\":"+fieldState+",\"gameState\":"+Quote(gameState)+",\"turn\":"+turn+",\"progress\":"+progress+",\"menu\":"+Quote(Convert.ToString(Field(source,"m_MenuMode")))+",\"menuActive\":"+Quote(Convert.ToString(Property(source,"menuActive")))+",\"back\":"+Point(Field(source,"m_BackCollider"))+",\"buttons\":"+buttons+",\"text\":"+Quote(text)+"}");
  } catch(Exception error) {Debug.Log("RTD_FLOW_ERROR "+error.GetType().Name+":"+error.Message);}
 }
}

