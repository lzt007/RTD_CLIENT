using System;

// Detached numeric estimate for input selection. Never calls Character.GetHopeDamage:
// that method calls CheckWeakness, which writes BattleManager.WeakDelay.
// Matches its deterministic formula, not randomized/critical actual damage.
public static class RTDBattleEstimate {
 public static int Damage(int attack,int attributeDamage,int killer,int defence,int weakness,
                          float[] scales,float chainSupport,int count) {
  int baseline=Math.Max((int)((float)Math.Pow(attack,.95f)+killer)+attributeDamage+weakness-defence,0);
  int result=0;
  for(int i=0;i<count;i++) {
   float scale=scales[Math.Min(i,7)]+chainSupport;
   result+=(int)(baseline*(scale>1f?1f:scale));
  }
  return result;
 }
 public static bool Weakness(int main,int sub,int enemy) {
  int required=enemy==2?3:enemy==3?2:enemy==4?5:enemy==5?4:0;
  return required!=0&&(main==required||sub==required);
 }
}
