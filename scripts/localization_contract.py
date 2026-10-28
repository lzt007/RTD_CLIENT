"""Shared UTF-8 JSON contract; token rules mirrored from translation exporter."""
import re
from collections import Counter
PLACEHOLDER = re.compile(r'\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}')
TAG = re.compile(r'\[(?:[-/A-Za-z0-9_=:#.]+)\]')


def format_tokens(value):
    tokens = []
    offset = 0
    while offset < len(value):
        if value.startswith(('{{', '}}'), offset):
            tokens.append(value[offset:offset + 2])
            offset += 2
        elif value[offset] == '{':
            match = PLACEHOLDER.match(value, offset)
            if not match:
                raise ValueError('invalid string.Format placeholder')
            tokens.append(match.group())
            offset = match.end()
        elif value[offset] == '}':
            raise ValueError('unescaped closing brace')
        else:
            offset += 1
    return Counter(tokens)


def validate(mapping):
    if not isinstance(mapping, dict):
        raise ValueError('Translation must be an object')
    clean = {}
    for source, target in mapping.items():
        if not isinstance(source, str) or not isinstance(target, str):
            raise ValueError('Translation entries must be strings')
        if not target.strip():
            continue
        if not source:
            raise ValueError('Empty source')
        if format_tokens(source) != format_tokens(target):
            raise ValueError('string.Format placeholders differ')
        if Counter(TAG.findall(source)) != Counter(TAG.findall(target)):
            raise ValueError('NGUI tags differ')
        clean[source] = target
    return clean
