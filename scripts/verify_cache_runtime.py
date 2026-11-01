"""Verify captured cold-cache and ordinary-restart Unity logs, without a device."""
import hashlib
import json
import pathlib
import re
import sys


def verify(log, cold):
    text = pathlib.Path(log).read_text(encoding='utf-8-sig')
    expected = {'AudioClipManager.unity3d'} | {f'{n}-{n+9}.unity3d' for n in range(1, 751, 10)}
    checks = re.findall(r'RTD_CACHE_CHECK hit=(True|False) version=(\d+) url=(\S+)', text)
    hits = {url.rsplit('/', 1)[-1] for hit, _, url in checks if hit == 'True'}
    assert expected <= hits, f'Missing cache hits: {sorted(expected-hits)}'
    assert all(hit == 'True' for hit, _, _ in checks), 'Cache miss in resource checks'
    loads = re.findall(r'RTD_CACHE_LOAD hit=(True|False) version=(\d+) url=(\S+)', text)
    assert loads and all(hit == 'True' for hit, _, _ in loads), 'Network fallback during load'
    assert 'Exception' not in text and 'verification failed' not in text, 'Runtime exception'
    downloads = re.findall(r'>Download Start_(\d+)', text)
    assert all(number == '0' for number in downloads), 'Unexpected uncached bundle branch'
    state = ('copied=180 skipped=0 reseed=True assertCache=True' if cold else
             'copied=0 skipped=180 reseed=False assertCache=False')
    assert state in text, f'Missing expected bootstrap state: {state}'
    return {'log': str(log), 'log_sha256': hashlib.sha256(pathlib.Path(log).read_bytes()).hexdigest(),
            'cache_hits': len(expected), 'cache_misses': 0, 'loads': len(loads),
            'all_loads_cached': True, 'cold_cache': cold}


if __name__ == '__main__':
    if len(sys.argv) != 3:
        raise SystemExit('Usage: verify_cache_runtime.py COLD_LOG RESTART_LOG')
    print(json.dumps([verify(sys.argv[1], True), verify(sys.argv[2], False)], indent=2))
