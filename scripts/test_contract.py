import unittest
from localization_contract import validate
class ContractTests(unittest.TestCase):
 def test_placeholders_tags_and_blank(self):
  self.assertEqual(validate({'源{0,3:000} [ff0000]':'中{0,3:000} [ff0000]','blank':' '}),{'源{0,3:000} [ff0000]':'中{0,3:000} [ff0000]'})
 def test_malformed_and_mismatched(self):
  for value in [[],{'a':1},{'':'中'},{'{0}':'{1}'},{'{0:000}':'{0}'},{'[ff0000]':'[00ff00]'},{'a':'x{'}]:
   with self.subTest(value=value):
    with self.assertRaises(ValueError):validate(value)
if __name__=='__main__':unittest.main()
