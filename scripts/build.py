"""Standalone original-APK maintenance chain. Does not contact or install devices."""
import argparse
import pathlib,json,os,shutil,subprocess,zipfile,hashlib,struct,copy,re,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
from native_input import patch_native,NATIVE_MEMBER
from android_launcher import patch_launcher,set_debuggable
from localization_contract import validate

def sha(b):return hashlib.sha256(b).hexdigest()
def path(s):
 p=pathlib.Path(s);return p if p.is_absolute() else ROOT/p
def run(args,cwd=ROOT):
 print('>',str(args[0]),' '.join(str(a) for a in args[1:]),flush=True)
 subprocess.run([str(a) for a in args],cwd=cwd,check=True)
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
  if not all((candidate/'bin'/name).is_file() for name in (('java.exe','keytool.exe') if os.name=='nt' else ('java','keytool'))):
   raise ValueError('Configured tools.javaHome must contain platform java and keytool executables')
  return candidate
 candidates=[]
 if os.environ.get('JAVA_HOME'):candidates.append(pathlib.Path(os.environ['JAVA_HOME']))
 executable=shutil.which('java.exe') or shutil.which('java')
 if executable:candidates.append(pathlib.Path(executable).resolve().parent.parent)
 for candidate in candidates:
  if all((candidate/'bin'/name).is_file() for name in (('java.exe','keytool.exe') if os.name=='nt' else ('java','keytool'))):return candidate
 raise ValueError('JDK missing: set tools.javaHome, JAVA_HOME, or put JDK bin containing java/keytool on PATH')

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--config',default='rtd.config.json');parser.add_argument('--language',choices=['ja','zh-TW','zh-CN'],default='ja');args=parser.parse_args()
 config_path=path(args.config);c=json.loads(config_path.read_text(encoding='utf-8-sig'));p=c['paths'];t=c['tools'];net=c['network']
 log_level=c.get('logging',{}).get('level','debug')
 if log_level not in ('debug','info','warning','error','none'):raise ValueError('logging.level must be debug/info/warning/error/none')
 build=path(p['build']);build.mkdir(parents=True,exist_ok=True);dist=path(p['dist']);dist.mkdir(parents=True,exist_ok=True)
 if args.language=='ja':
  p=dict(p);p['translation']=str(build/'translation-ja.json');path(p['translation']).write_text('{}',encoding='utf-8')
 translation_path=translation_input(p['translation'])
 translations=validate(json.loads(translation_path.read_text(encoding='utf-8-sig'),object_pairs_hook=unique))
 source=path(p['originalApk']);original_sha=sha(source.read_bytes())
 if original_sha!='1e95f5a91c6d3ead09c7f5462ace6238deda87392621f07758c5dc7439d784ec':raise ValueError('Original APK identity differs; audit patch shapes before supporting a new original')
 managed=build/'original-managed';managed.mkdir(exist_ok=True)
 with zipfile.ZipFile(source) as z:
  for name in z.namelist():
   if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):(managed/pathlib.Path(name).name).write_bytes(z.read(name))
  native,meta=patch_native(z.read(NATIVE_MEMBER));scene=bytearray(z.read('assets/bin/Data/level0'))
  manifest=set_debuggable(patch_launcher(z.read('AndroidManifest.xml')),c.get('android',{}).get('debuggable',False))
  mono=z.read('assets/libs/armeabi-v7a/libmono.so')
 old=bytes(scene[24320:24404]);assert len(old)==84 and struct.unpack_from('<I',old,72)[0]==20021
 def string(s):
  b=s.encode();d=struct.pack('<I',len(b))+b;return d+b'\0'*((-len(d))%4)
 new=old[:24]+struct.pack('<IIII',0,20021,0,3)+string('ms01_sla_lv1')+string('ms01_sla_fire_lv1')+string('');assert len(new)==84;scene[24320:24404]=new
 dotnet=path(t['dotnet']) if t['dotnet'] else shutil.which('dotnet');java=java_home(t['javaHome'])
 sdk=path(t['androidSdk']) if t['androidSdk'] else pathlib.Path(os.environ.get('ANDROID_HOME',str(pathlib.Path.home()/'AppData/Local/Android/Sdk')))
 if not t['androidSdk'] and not (sdk/'build-tools').is_dir() and (ROOT/'tools/android-sdk/build-tools').is_dir():sdk=ROOT/'tools/android-sdk'
 if not (sdk/'build-tools').is_dir():raise ValueError('Android SDK build-tools missing: set tools.androidSdk or ANDROID_HOME')
 if not (java/('bin/java.exe' if os.name=='nt' else 'bin/java')).is_file():raise ValueError('JDK missing: set tools.javaHome or JAVA_HOME')
 bt=sorted(x for x in (sdk/'build-tools').iterdir() if x.is_dir())[-1];keytool=java/('bin/keytool.exe' if os.name=='nt' else 'bin/keytool');apksigner=bt/'lib/apksigner.jar';zipalign=bt/('zipalign.exe' if os.name=='nt' else 'zipalign')
 compiler=sorted((pathlib.Path(dotnet).parent/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
 def compile(name,files,extra=()):
  arguments=['/nostdlib','/target:library','/optimize+','/deterministic+','/out:'+str(build/name),'/reference:'+str(managed/'mscorlib.dll'),'/reference:'+str(managed/'UnityEngine.dll'),*extra,*[ROOT/'src'/f for f in files]]
  response=build/(name+'.rsp');response.write_text('\n'.join(chr(34)+str(a)+chr(34) for a in arguments),encoding='utf-8')
  run([dotnet,compiler,'/nologo','/noconfig','@'+str(response)])
 cache_files=sorted(x for x in path(p['resourceCache']).rglob('*') if x.is_file())
 if not cache_files:raise ValueError('Embedded resource cache is empty')
 cache_index=[];cache_resources=[]
 for f in cache_files:
  relative=f.relative_to(path(p['resourceCache'])).as_posix()
  if not relative.startswith('Shared/'):raise ValueError('Unexpected cache path: '+relative)
  cache_index.append(relative+'|'+str(f.stat().st_size)+'|'+sha(f.read_bytes()))

 (build/'EmbeddedCacheIndex.cs').write_text('public static class EmbeddedCacheIndex { public static readonly string[] Files = new string[]{'+','.join(json.dumps(x) for x in cache_index)+'};}',encoding='utf-8')
 compile('RTDFlow.dll',['RTDFlow.cs','RTDLocalStore.cs','RTDSkillAttributes.cs','RTDBattleEstimate.cs','RTDNetworkTrace.cs','RTDEmbeddedCache.cs',str(build/'EmbeddedCacheIndex.cs')],cache_resources)
 compile('RTDRestoreCache.dll',['RTDRestoreCache.cs'],['/define:RTD_LEGACY','/reference:'+str(managed/'Assembly-CSharp.dll')])
 resources=['/resource:'+str(path(p['translation']))+',rtd.translation.json']
 font_paths=[path(p['fontPng']),path(p['fontGlyphs'])] if args.language!='ja' else [build/'no-font-ja.png',build/'no-font-ja.tsv']
 if args.language!='ja' and not all(x.exists() for x in font_paths):raise ValueError('Chinese build requires font PNG and TSV')
 if any(x.exists() for x in font_paths):
  if not all(x.exists() for x in font_paths):raise ValueError('fontPng and fontGlyphs must both exist')
  resources+=['/resource:'+str(font_paths[0])+',rtd.font.png','/resource:'+str(font_paths[1])+',rtd.font.tsv']
 embedded=['public static class EmbeddedLocalization { public static byte[] Read(string name) { switch(name) {']
 for filename,filepath in [('translation.json',path(p['translation'])),('font.png',font_paths[0]),('font.tsv',font_paths[1])]:
  if filepath.exists():embedded.append('case '+json.dumps(filename)+':return new byte[]{'+','.join(str(b) for b in filepath.read_bytes())+'};')
 embedded.append('default:return null;}}}')
 (build/'EmbeddedLocalization.cs').write_text(''.join(embedded))
 compile('RTDLocalization.dll',['TranslationMap.cs','RTDLocalization.cs',str(build/'EmbeddedLocalization.cs')],['/reference:'+str(managed/'Assembly-CSharp.dll')]+(['/define:RTD_JAPANESE'] if args.language=='ja' else []))
 def patch(proj,args):run([dotnet,'run','-p:RTDCecilPath='+str(path(t['cecil'])),'--project',ROOT/'src'/proj,'--',*args],cwd=managed)
 for name in ['restored.dll','notice.dll','notice-report.json','localized.dll']:(build/name).unlink(missing_ok=True)
 patch('TracePatcher',[managed,build/'Assembly-CSharp-trace.dll','no-social','resource','offline','local-api'])
 patch('RestoreCachePatcher',['apply',build/'Assembly-CSharp-trace.dll',build/'RTDRestoreCache.dll',build/'restored.dll'])
 patch('StartupNoticePatcher',['apply',build/'restored.dll',build/'notice.dll',build/'notice-report.json'])
 patch('LocalizationPatcher',[build/'notice.dll',build/'RTDLocalization.dll',managed,build/'localized.dll'])
 # Rewrite all patch-injected roots after structural patches; original sources remain untouched.
 patch('EndpointPatcher',[build/'localized.dll',build/'Assembly-CSharp.dll',str(config_path)])
 patch('TracePatcher',['embed-cache-hook',build/'Assembly-CSharp.dll',build/'RTDFlow.dll'])
 patch('LoaderTests',[])
 patch('LocalStoreTests',[])
 patch('LocalizationTests',[managed/'Assembly-CSharp.dll',build/'Assembly-CSharp.dll'])
 patch('TracePatcher',['verify-local-platform',build/'Assembly-CSharp.dll']);patch('TracePatcher',['verify-local-battle-restore',build/'Assembly-CSharp.dll'])
 patch('TracePatcher',['verify-network-trace',build/'Assembly-CSharp.dll'])
 patch('TracePatcher',['verify-local-store',build/'Assembly-CSharp.dll'])
 for assembly in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass-trace.dll','RTDFlow.dll','RTDRestoreCache.dll','RTDLocalization.dll']:
  patch('TracePatcher',['filter-logs',build/assembly,log_level])
 shutil.copyfile(build/'endpoint-audit.json',dist/'endpoint-audit.json')
 replacements={NATIVE_MEMBER:native,'assets/bin/Data/level0':bytes(scene),'assets/bin/Data/Managed/Assembly-CSharp.dll':(build/'Assembly-CSharp.dll').read_bytes(),'assets/bin/Data/Managed/Assembly-CSharp-firstpass.dll':(build/'Assembly-CSharp-firstpass-trace.dll').read_bytes()}
 for helper in ['RTDFlow.dll','RTDRestoreCache.dll','RTDLocalization.dll']:replacements['assets/bin/Data/Managed/'+helper]=(build/helper).read_bytes()
 for f in cache_files:replacements['assets/rtd-cache/'+f.relative_to(path(p['resourceCache'])).as_posix()]=f.read_bytes()
 replacements['AndroidManifest.xml']=manifest
 # Replace obsolete native loader shells with the actual original engine libraries.
 replacements['lib/armeabi-v7a/libunity.so']=native
 replacements['lib/armeabi-v7a/libmono.so']=mono
 replacements['assets/rtd-localization/translation.json']=path(p['translation']).read_bytes()
 for filename,filepath in zip(['font.png','font.tsv'],font_paths):
  if filepath.exists():replacements['assets/rtd-localization/'+filename]=filepath.read_bytes()
 unsigned=build/'unsigned.apk';aligned=build/'aligned.apk';out=dist/('rtd-client-'+args.language+'.apk')
 with zipfile.ZipFile(source) as src,zipfile.ZipFile(unsigned,'w',zipfile.ZIP_DEFLATED) as dst:
  for info in src.infolist():
   if info.filename.upper().startswith('META-INF/'):continue
   dst.writestr(copy.copy(info),replacements.pop(info.filename,src.read(info)))
  for name,value in replacements.items():dst.writestr(name,value)
 run([zipalign,'-f','-p','4',unsigned,aligned]);key=path(p['keystore']);key.parent.mkdir(parents=True,exist_ok=True);sign=c['signing']
 if not key.exists():run([keytool,'-genkeypair','-keystore',key,'-storepass',sign['storePassword'],'-keypass',sign['keyPassword'],'-alias',sign['alias'],'-keyalg','RSA','-keysize','2048','-validity','3650','-dname','CN=RTD Local Development','-storetype','JKS'])
 run([java/('bin/java.exe' if os.name=='nt' else 'bin/java'),'-jar',apksigner,'sign','--ks',key,'--ks-key-alias',sign['alias'],'--ks-pass','pass:'+sign['storePassword'],'--key-pass','pass:'+sign['keyPassword'],'--min-sdk-version','14','--out',out,aligned])
 run([zipalign,'-c','-p','4',out]);run([java/('bin/java.exe' if os.name=='nt' else 'bin/java'),'-jar',apksigner,'verify','--verbose','--print-certs',out])
 with zipfile.ZipFile(out) as z:
  assert z.read(NATIVE_MEMBER)==native
  assert z.read('lib/armeabi-v7a/libunity.so')==native
  assert z.read('lib/armeabi-v7a/libmono.so')==mono
  assert z.read('AndroidManifest.xml')==manifest
  for f in cache_files:assert z.read('assets/rtd-cache/'+f.relative_to(path(p['resourceCache'])).as_posix())==f.read_bytes()
  with zipfile.ZipFile(source) as original:assert z.read('classes.dex')==original.read('classes.dex')
  assert len(z.namelist())==len(set(z.namelist()))
  for site in meta['call_sites']:assert struct.unpack_from('<I',native,site)[0]>>24==0xea
 meta.update(original_apk_sha256=original_sha,apk_sha256=sha(out.read_bytes()),output=report_path(out),translation_entries=len(translations),network=net,logging_level=log_level,verification='zipalign/apksigner/native-sites/managed-shapes; no device/runtime validation')
 meta.update(language=args.language)
 meta.update(android_debuggable=c.get('android',{}).get('debuggable',False))
 meta.update(embedded_cache_files=len(cache_files),embedded_cache_bytes=sum(f.stat().st_size for f in cache_files))
 meta.update(startup_billing_checks='bypassed in local mode',network_trace='RTD_NET; five WWW POST sites',endpoint_audit=report_path(dist/'endpoint-audit.json'))
 (dist/('build-report-'+args.language+'.json')).write_text(json.dumps(meta,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(meta,ensure_ascii=False,indent=2))
def unique(pairs):
 d={}
 for k,v in pairs:
  if k in d:raise ValueError('Duplicate translation source')
  d[k]=v
 return d
if __name__=='__main__':main()
