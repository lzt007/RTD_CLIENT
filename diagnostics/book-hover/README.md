# Book hover diagnostic candidate

This helper is **not part of the normal APK build**. Use a separately signed
candidate copied from the current Chinese APK; keep the normal APK unchanged.
No device reboot, uninstall, account edit or server configuration change is needed.

Compile HoverProbe.cs against the original managed mscorlib.dll and UnityEngine.dll
with `mcs -target:library -nostdlib`; compile HoverPatcher.cs with Mono.Cecil and
System.Core. The patcher takes a managed input directory, helper DLL, and distinct
output Assembly-CSharp.dll. Package the patched assembly and RTDHoverProbe.dll
into the candidate APK, then align/sign using the ignored local signing config.

Before starting the app, put the **exact** ms00_book_el_03.unity3d under the app's
persistent data directory as `hover-book.unity3d`. Record its SHA256 and revision.
The helper creates a temporary offscreen clone and samples three phases:

1. Root hover autoplay alone.
2. Original pc_wait_stk_01 added and CrossFade("Wait") on move, as in the original
   character-enemy code path.
3. Child animation stopped, without changing root playback.

`RTD_HOVER_PROBE` logs the sampled bone Y range, move Y and both animation states.
Old v7 samples move; v8 samples book_hover. A moving root clip alone is insufficient:
**the skin bone must continue moving while the child Wait state is playing**.
The helper destroys its clone after the experiment. `RTD_HOVER_LIVE` additionally
samples active book instances once per second, recognizing the nested hover path
even if the game renamed the root; it does not change
their animation or transform. No account/session values or endpoints are logged.

After verification, install the normal Chinese APK over the candidate and remove
hover-book.unity3d. Do not ship the helper, patched assembly, or probe hook.
