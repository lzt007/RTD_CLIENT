"""Export verified original-cache identities for an optional first-run migration.

Publication manifest and output are local build inputs, not deployment config.
No server is contacted. An absent baseline remains correct: first use downloads.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path, PureWindowsPath
import UnityPy


def digest(data):return hashlib.sha256(data).hexdigest()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('publication',type=Path)
    parser.add_argument('identities',type=Path)
    parser.add_argument('--cache',required=True,type=Path)
    parser.add_argument('--output',required=True,type=Path)
    args=parser.parse_args()
    resources=json.loads(args.publication.read_text())['resources']
    entries=[]
    with args.identities.open(encoding='utf-8-sig') as stream:
        for row in csv.DictReader(stream):
            identifier=row['name']+'.unity3d'
            if not row['requested_version'] or identifier not in resources:continue
            source=args.publication.parent/resources[identifier]['source']
            data=source.read_bytes();assets=list(UnityPy.load(data).assets)
            if len(assets)!=1:continue
            payload=assets[0].reader.bytes;identity=digest(payload)
            relative='Shared/'+row['cache_dir']+'/'+PureWindowsPath(row['payload']).name
            target=args.cache/relative
            if identity!=row['payload_sha256'] or not target.is_file() or digest(target.read_bytes())!=identity:continue
            entries.append(dict(id=identifier,sha256=digest(data),size=len(data),legacyVersion=int(row['requested_version']),legacyPayload=relative,legacyPayloadHash=identity,legacyLength=len(payload)))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(entries,sort_keys=True)+'\n')
    print('Verified baseline entries:',len(entries))


if __name__=='__main__':main()
