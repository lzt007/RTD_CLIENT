"""Structural tests only: never execute shipped game code on the host."""
from pathlib import Path
import subprocess
import tempfile
ROOT=Path(__file__).resolve().parents[1]
cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll'
build=ROOT/'build'
exe=build/'resource-update-tests/Patcher.exe'
subprocess.run(['mcs','-r:'+cecil,'-r:System.Core','-out:'+str(exe),str(ROOT/'src/ResourceUpdatePatcher/Program.cs')],check=True)
def run(*args,ok=True):
 result=subprocess.run(['mono',str(exe),*map(str,args)],capture_output=True,text=True)
 if (result.returncode==0)!=ok:raise AssertionError(result.stdout+result.stderr)
with tempfile.TemporaryDirectory(dir=build) as directory:
 output=Path(directory)/'patched.dll'
 run('apply',build/'original-managed/Assembly-CSharp.dll',build/'RTDResourceCore-check.dll',build/'RTDResourceGameBridge-check.dll',output)
 run('verify',output)
 run('apply',output,build/'RTDResourceCore-check.dll',build/'RTDResourceGameBridge-check.dll',Path(directory)/'duplicate.dll',ok=False)
 run('apply',build/'original-managed/Assembly-CSharp-firstpass.dll',build/'RTDResourceCore-check.dll',build/'RTDResourceGameBridge-check.dll',Path(directory)/'unknown.dll',ok=False)
print('PASS patch gates, load-site coverage, duplicate and unknown-shape rejection')
