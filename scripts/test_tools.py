import os,tempfile,pathlib,unittest
from unittest.mock import patch
from build import java_home,translation_input,report_path,ROOT
class DiscoveryTests(unittest.TestCase):
 def test_path_jdk_without_java_home(self):
  with tempfile.TemporaryDirectory() as folder:
   root=pathlib.Path(folder);(root/'bin').mkdir()
   for name in ['java.exe','keytool.exe']:(root/'bin'/name).touch()
   with patch.dict(os.environ, {'JAVA_HOME':''}), patch('build.shutil.which', return_value=str(root/'bin/java.exe')):
    self.assertEqual(java_home(),root.resolve())
 def test_missing_translation_initializes_empty_and_preserves_existing(self):
  with tempfile.TemporaryDirectory() as folder:
   target=pathlib.Path(folder)/'nested/translation.json'
   self.assertEqual(translation_input(str(target)).read_text(encoding='utf-8'),'{}\n')
   target.write_text('{"x":"y"}',encoding='utf-8')
   self.assertEqual(translation_input(str(target)).read_text(encoding='utf-8'),'{"x":"y"}')
 def test_external_dist_report_uses_absolute_path(self):
  external=ROOT.parent/'external-client-dist'/'rtd-client.apk'
  self.assertEqual(report_path(external),str(external))
  self.assertEqual(report_path(ROOT/'dist/rtd-client.apk'),str(pathlib.Path('dist/rtd-client.apk')))
 def test_explicit_invalid_is_not_silently_replaced(self):
  with self.assertRaises(ValueError):java_home('missing-jdk-for-test')
 def test_no_environment_or_path_has_clear_error(self):
  with patch.dict(os.environ, {'JAVA_HOME':''}),patch('build.shutil.which',return_value=None):
   with self.assertRaisesRegex(ValueError,'JDK missing'):java_home()
if __name__=='__main__':unittest.main()

