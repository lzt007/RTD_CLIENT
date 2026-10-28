using System;
using System.Reflection;

// Readonly helper, compiled into RTDFlow against the original stripped runtime.
public static class RTDSkillAttributes {
 public static bool Matches(Assembly game,object characterData,int serverAttribute) {
  object attribute=characterData.GetType().GetMethod("GetMainAttribute").Invoke(characterData,new object[]{true});
  object converted=game.GetType("ConvertClientToServer").GetMethod("ConvertAttribute").Invoke(null,new object[]{attribute});
  return Convert.ToInt32(converted)==serverAttribute;
 }
}
