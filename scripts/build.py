"""Standalone original-APK maintenance chain. Does not contact or install devices."""
import pathlib,json,os,shutil,subprocess,zipfile,hashlib,struct,copy,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
from native_input import patch_native,NATIVE_MEMBER
from android_launcher import patch_launcher
from localization_contract import validate

def sha(b):return hashlib.sha256(b).hexdigest()
def path(s):
 p=pathlib.Path(s);return p if p.is_absolute() else ROOT/p
def run(args):
 print('>',str(args[0]),' '.join(str(a) for a in args[1:]),flush=True)
 subprocess.run([str(a) for a in args],cwd=ROOT,check=True)
def report_path(output):
 try:return str(pathlib.Path(output).relative_to(ROOT))
 except ValueError:return str(output)

def translation_input(configured):
 target=path(configured)
 if not target.exists():
  target.parent.mkdir(parents=True,exist_ok=True)
  shutil.copyfile(ROOT/'examples/translation.example.json',target)
 return target

def java_home(configured=''):
 if configured:
  candidate=path(configured)
  if not all((candidate/'bin'/name).is_file() for name in ('java.exe','keytool.exe')):
   raise ValueError('Configured tools.javaHome must contain bin/java.exe and bin/keytool.exe')
  return candidate
 candidates=[]
 if os.environ.get('JAVA_HOME'):candidates.append(pathlib.Path(os.environ['JAVA_HOME']))
 executable=shutil.which('java.exe') or shutil.which('java')
 if executable:candidates.append(pathlib.Path(executable).resolve().parent.parent)
 for candidate in candidates:
  if all((candidate/'bin'/name).is_file() for name in ('java.exe','keytool.exe')):return candidate
 raise ValueError('JDK missing: set tools.javaHome, JAVA_HOME, or put JDK bin containing java/keytool on PATH')

def main():
 c=json.loads((ROOT/'rtd.config.json').read_text(encoding='utf-8-sig'));p=c['paths'];t=c['tools'];net=c['network']
 build=path(p['build']);build.mkdir(parents=True,exist_ok=True);dist=path(p['dist']);dist.mkdir(parents=True,exist_ok=True)
 translation_path=translation_input(p['translation'])
 translations=validate(json.loads(translation_path.read_text(encoding='utf-8-sig'),object_pairs_hook=unique))
 source=path(p['originalApk']);original_sha=sha(source.read_bytes())
 if original_sha!='1e95f5a91c6d3ead09c7f5462ace6238deda87392621f07758c5dc7439d784ec':raise ValueError('Original APK identity differs; audit patch shapes before supporting a new original')
 managed=build/'original-managed';managed.mkdir(exist_ok=True)
 with zipfile.ZipFile(source) as z:
  for name in z.namelist():
   if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):(managed/pathlib.Path(name).name).write_bytes(z.read(name))
  native,meta=patch_native(z.read(NATIVE_MEMBER));scene=bytearray(z.read('assets/bin/Data/level0'))
  manifest=patch_launcher(z.read('AndroidManifest.xml'))
  mono=z.read('assets/libs/armeabi-v7a/libmono.so')
 old=bytes(scene[24320:24404]);assert len(old)==84 and struct.unpack_from('<I',old,72)[0]==20021
 def string(s):
  b=s.encode();d=struct.pack('<I',len(b))+b;return d+b'\0'*((-len(d))%4)
 new=old[:24]+struct.pack('<IIII',0,20021,0,3)+string('ms01_sla_lv1')+string('ms01_sla_fire_lv1')+string('');assert len(new)==84;scene[24320:24404]=new
 dotnet=path(t['dotnet']) if t['dotnet'] else shutil.which('dotnet');java=java_home(t['javaHome'])
 sdk=path(t['androidSdk']) if t['androidSdk'] else pathlib.Path(os.environ.get('ANDROID_HOME',str(pathlib.Path.home()/'AppData/Local/Android/Sdk')))
 if not t['androidSdk'] and not (sdk/'build-tools').is_dir() and (ROOT/'tools/android-sdk/build-tools').is_dir():sdk=ROOT/'tools/android-sdk'
 if not (sdk/'build-tools').is_dir():raise ValueError('Android SDK build-tools missing: set tools.androidSdk or ANDROID_HOME')
 if not (java/'bin/java.exe').is_file():raise ValueError('JDK missing: set tools.javaHome or JAVA_HOME')
 bt=sorted((sdk/'build-tools').iterdir())[-1];keytool=java/'bin/keytool.exe';apksigner=bt/'lib/apksigner.jar';zipalign=bt/'zipalign.exe'
 compiler=sorted((pathlib.Path(dotnet).parent/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
 def compile(name,files,extra=()):
  run([dotnet,compiler,'/nologo','/noconfig','/nostdlib','/target:library','/optimize+','/deterministic+','/out:'+str(build/name),'/reference:'+str(managed/'mscorlib.dll'),'/reference:'+str(managed/'UnityEngine.dll'),*extra,*[ROOT/'src'/f for f in files]])
 compile('RTDFlow.dll',['RTDFlow.cs','RTDSkillAttributes.cs','RTDBattleEstimate.cs','RTDNetworkTrace.cs'])
 compile('RTDRestoreCache.dll',['RTDRestoreCache.cs'],['/define:RTD_LEGACY','/reference:'+str(managed/'Assembly-CSharp.dll')])
 resources=['/resource:'+str(path(p['translation']))+',rtd.translation.json']
 font_paths=[path(p['fontPng']),path(p['fontGlyphs'])]
 if any(x.exists() for x in font_paths):
  if not all(x.exists() for x in font_paths):raise ValueError('fontPng and fontGlyphs must both exist')
  resources+=['/resource:'+str(font_paths[0])+',rtd.font.png','/resource:'+str(font_paths[1])+',rtd.font.tsv']
 embedded=['public static class EmbeddedLocalization { public static byte[] Read(string name) { switch(name) {']
 for filename,filepath in [('translation.json',path(p['translation'])),('font.png',font_paths[0]),('font.tsv',font_paths[1])]:
  if filepath.exists():embedded.append('case '+json.dumps(filename)+':return new byte[]{'+','.join(str(b) for b in filepath.read_bytes())+'};')
 embedded.append('default:return null;}}}')
 (build/'EmbeddedLocalization.cs').write_text(''.join(embedded))
 compile('RTDLocalization.dll',['TranslationMap.cs','RTDLocalization.cs',str(build/'EmbeddedLocalization.cs')],['/reference:'+str(managed/'Assembly-CSharp.dll')])
 def patch(proj,args):run([dotnet,'run','-p:RTDCecilPath='+str(path(t['cecil'])),'--project',ROOT/'src'/proj,'--',*args])
 for name in ['restored.dll','notice.dll','notice-report.json','localized.dll']:(build/name).unlink(missing_ok=True)
 patch('TracePatcher',[managed,build/'Assembly-CSharp-trace.dll','no-social','resource','offline','local-api'])
 patch('RestoreCachePatcher',['apply',build/'Assembly-CSharp-trace.dll',build/'RTDRestoreCache.dll',build/'restored.dll'])
 patch('StartupNoticePatcher',['apply',build/'restored.dll',build/'notice.dll',build/'notice-report.json'])
 patch('LocalizationPatcher',[build/'notice.dll',build/'RTDLocalization.dll',managed,build/'localized.dll'])
 # Rewrite all patch-injected roots after structural patches; original sources remain untouched.
 patch('EndpointPatcher',[build/'localized.dll',build/'Assembly-CSharp.dll',str(ROOT/'rtd.config.json')])
 patch('LoaderTests',[])
 patch('LocalizationTests',[managed/'Assembly-CSharp.dll',build/'Assembly-CSharp.dll'])
 patch('TracePatcher',['verify-local-platform',build/'Assembly-CSharp.dll']);patch('TracePatcher',['verify-local-battle-restore',build/'Assembly-CSharp.dll'])
 patch('TracePatcher',['verify-network-trace',build/'Assembly-CSharp.dll'])
 shutil.copyfile(build/'endpoint-audit.json',dist/'endpoint-audit.json')
 replacements={NATIVE_MEMBER:native,'assets/bin/Data/level0':bytes(scene),'assets/bin/Data/Managed/Assembly-CSharp.dll':(build/'Assembly-CSharp.dll').read_bytes(),'assets/bin/Data/Managed/Assembly-CSharp-firstpass.dll':(build/'Assembly-CSharp-firstpass-trace.dll').read_bytes()}
 for helper in ['RTDFlow.dll','RTDRestoreCache.dll','RTDLocalization.dll']:replacements['assets/bin/Data/Managed/'+helper]=(build/helper).read_bytes()
 replacements['AndroidManifest.xml']=manifest
 # Replace obsolete native loader shells with the actual original engine libraries.
 replacements['lib/armeabi-v7a/libunity.so']=native
 replacements['lib/armeabi-v7a/libmono.so']=mono
 replacements['assets/rtd-localization/translation.json']=path(p['translation']).read_bytes()
 for filename,filepath in zip(['font.png','font.tsv'],font_paths):
  if filepath.exists():replacements['assets/rtd-localization/'+filename]=filepath.read_bytes()
 unsigned=build/'unsigned.apk';aligned=build/'aligned.apk';out=dist/'rtd-client.apk'
 with zipfile.ZipFile(source) as src,zipfile.ZipFile(unsigned,'w',zipfile.ZIP_DEFLATED) as dst:
  for info in src.infolist():
   if info.filename.upper().startswith('META-INF/'):continue
   dst.writestr(copy.copy(info),replacements.pop(info.filename,src.read(info)))
  for name,value in replacements.items():dst.writestr(name,value)
 run([zipalign,'-f','-p','4',unsigned,aligned]);key=path(p['keystore']);key.parent.mkdir(parents=True,exist_ok=True);sign=c['signing']
 if not key.exists():run([keytool,'-genkeypair','-keystore',key,'-storepass',sign['storePassword'],'-keypass',sign['keyPassword'],'-alias',sign['alias'],'-keyalg','RSA','-keysize','2048','-validity','3650','-dname','CN=RTD Local Development','-storetype','JKS'])
 run([java/'bin/java.exe','-jar',apksigner,'sign','--ks',key,'--ks-key-alias',sign['alias'],'--ks-pass','pass:'+sign['storePassword'],'--key-pass','pass:'+sign['keyPassword'],'--min-sdk-version','14','--out',out,aligned])
 run([zipalign,'-c','-p','4',out]);run([java/'bin/java.exe','-jar',apksigner,'verify','--verbose','--print-certs',out])
 with zipfile.ZipFile(out) as z:
  assert z.read(NATIVE_MEMBER)==native
  assert z.read('lib/armeabi-v7a/libunity.so')==native
  assert z.read('lib/armeabi-v7a/libmono.so')==mono
  assert z.read('AndroidManifest.xml')==manifest
  with zipfile.ZipFile(source) as original:assert z.read('classes.dex')==original.read('classes.dex')
  assert len(z.namelist())==len(set(z.namelist()))
  for site in meta['call_sites']:assert struct.unpack_from('<I',native,site)[0]>>24==0xea
 meta.update(original_apk_sha256=original_sha,apk_sha256=sha(out.read_bytes()),output=report_path(out),translation_entries=len(translations),network=net,verification='zipalign/apksigner/native-sites/managed-shapes; no device/runtime validation')
 meta.update(startup_billing_checks='bypassed in local mode',network_trace='RTD_NET; five WWW POST sites',endpoint_audit=report_path(dist/'endpoint-audit.json'))
 (dist/'build-report.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(meta,ensure_ascii=False,indent=2))
def unique(pairs):
 d={}
 for k,v in pairs:
  if k in d:raise ValueError('Duplicate translation source')
  d[k]=v
 return d
if __name__=='__main__':main()
