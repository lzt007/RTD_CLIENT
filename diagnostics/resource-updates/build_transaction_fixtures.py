"""Build isolated transaction fixtures; never overwrite a game resource.

Usage: Python with UnityPy, SCRIPT original-book.unity3d ignored-output-directory.
The two logical fixture resources have independent internal identities. Versions
of each resource deliberately retain one CAB identity to exercise handle reload.
"""
import hashlib
import json
from pathlib import Path
import sys
import UnityPy

source=Path(sys.argv[1]);output=Path(sys.argv[2]);output.mkdir(parents=True,exist_ok=True)
for name in ('a','b'):
    for version in (1,2):
        env=UnityPy.load(source.read_bytes());changed=0
        for obj in env.objects:
            if obj.type.name!='AssetBundle':continue
            bundle=obj.read();bundle.m_Name=f'rtd_transaction_fixture_{name}_v{version}'
            for export,info in bundle.m_Container:
                if info.asset.type.name=='GameObject':
                    value=info.asset.read();value.m_Name=f'rtd_transaction_fixture_{name}_v{version}';value.save();changed+=1
            bundle.save()
        if changed!=1:raise ValueError('Expected exactly one audited book prefab export')
        for original in list(env.file.files):
            asset=env.file.files.pop(original)
            unique='CustomAssetBundle-'+hashlib.md5(('rtd-transaction-fixture-'+name+original).encode()).hexdigest()
            asset.name=unique;env.file.files[unique]=asset
        data=env.file.save();target=output/f'{name}-v{version}.unity3d'
        if target.exists():raise FileExistsError(target)
        target.write_bytes(data)
        loaded=UnityPy.load(data)
        print(target.name,len(data),hashlib.sha256(data).hexdigest())
(output/'b-invalid.unity3d').write_bytes(b'intentionally invalid isolated transaction fixture')
(output/'README.json').write_text(json.dumps({'scope':'Isolated additional catalog IDs only; original game bundles unchanged','source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'export':'ast_ms00_book_el_05'},sort_keys=True)+'\n')
