"""Run pure resource-update checks against a TEST-ONLY retarget of shipped LitJson.

Never copy the retargeted firstpass assembly into an APK. The game retains its
original reference identities; host Mono tests need desktop System references.
"""
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
build=ROOT/'build/resource-update-tests'
build.mkdir(parents=True,exist_ok=True)
managed=ROOT/'build/original-managed'
cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll'
def run(args):subprocess.run([str(x) for x in args],check=True,cwd=ROOT)
run(['mcs','-out:'+str(build/'RetargetTest.exe'),'-r:'+cecil,ROOT/'diagnostics/resource-updates/RetargetTest.cs'])
run(['mono',build/'RetargetTest.exe',managed/'Assembly-CSharp-firstpass.dll',build/'Assembly-CSharp-firstpass.dll'])
for name,files in [('Transfer',['TransferPolicy']),('Store',['PlatformFiles','Store']),('Protocol',['Protocol']),('Planner',['Protocol','Planner']),('Transaction',['Protocol','Planner','PlatformFiles','Store','Transaction'])]:
 output=build/(name+'Tests.exe')
 run(['mcs','-out:'+str(output),'-r:'+str(managed/'Assembly-CSharp-firstpass.dll'),*[ROOT/'src/ResourceUpdates'/(x+'.cs') for x in files],ROOT/'src'/('ResourceUpdate'+name+'Tests.cs')])
 run(['mono',output])
