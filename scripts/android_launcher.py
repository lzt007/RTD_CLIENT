"""Move the original launcher filter to the existing Unity native activity."""
import struct

def patch_launcher(data):
    b = bytes(data)
    assert struct.unpack_from('<H', b)[0] == 3
    chunks = []
    pos = 8
    strings = None
    while pos < len(b):
        kind, header, size = struct.unpack_from('<HHI', b, pos)
        assert size >= header and pos + size <= len(b)
        chunk = b[pos:pos + size]
        if kind == 1:
            count, _, flags, start = struct.unpack_from('<IIII', chunk, 8)
            assert not flags & 0x100, 'Expected original UTF-16 string pool'
            strings = []
            for i in range(count):
                p = start + struct.unpack_from('<I', chunk, header + i * 4)[0]
                length = struct.unpack_from('<H', chunk, p)[0]
                assert length < 0x8000
                strings.append(chunk[p + 2:p + 2 + length * 2].decode('utf-16le'))
        chunks.append((kind, chunk))
        pos += size
    stack = []
    activities = {}
    filters = []
    def is_main_action(item):
        kind, chunk = item
        if kind != 0x102 or strings[struct.unpack_from('<I', chunk, 20)[0]] != 'action':
            return False
        offset, width, count = struct.unpack_from('<HHH', chunk, 24)
        for j in range(count):
            p = 16 + offset + j * width
            if strings[struct.unpack_from('<I', chunk, p + 4)[0]] == 'name':
                return strings[struct.unpack_from('<I', chunk, p + 16)[0]] == 'android.intent.action.MAIN'
        return False
    for i, (kind, chunk) in enumerate(chunks):
        if kind == 0x102:
            name = strings[struct.unpack_from('<I', chunk, 20)[0]]
            if name == 'activity':
                offset, width, count = struct.unpack_from('<HHH', chunk, 24)
                for j in range(count):
                    p = 16 + offset + j * width
                    if strings[struct.unpack_from('<I', chunk, p + 4)[0]] == 'name':
                        value = strings[struct.unpack_from('<I', chunk, p + 16)[0]]
                        activities[value] = [i, None]
            stack.append((name, i))
        elif kind == 0x103:
            name, first = stack.pop()
            if name == 'intent-filter' and any(x[0] == 'activity' and x[1] == activities.get('.CARewardPluginActivity', [-1])[0] for x in stack):
                if any(is_main_action(item) for item in chunks[first:i]):
                    filters.append((first, i))
            if name == 'activity':
                for span in activities.values():
                    if span[0] == first:
                        span[1] = i
    assert len(filters) == 1, 'Original advertising launcher shape changed'
    target = activities['com.prime31.UnityPlayerNativeActivity'][1]
    first, last = filters[0]
    moved = chunks[first:last + 1]
    result = []
    for i, item in enumerate(chunks):
        if i == target:
            result.extend(moved)
        if not first <= i <= last:
            result.append(item)
    output = b[:8] + b''.join(chunk for _, chunk in result)
    assert len(output) == len(b)
    return output


def set_debuggable(data, enabled):
    """Set the existing typed Android application debuggable attribute."""
    if not isinstance(enabled, bool):
        raise ValueError('android.debuggable must be a boolean')
    b = bytearray(data)
    pos = 8
    strings = []
    changed = 0
    while pos < len(b):
        kind, header, size = struct.unpack_from('<HHI', b, pos)
        if kind == 1:
            count, _, flags, start = struct.unpack_from('<IIII', b, pos + 8)
            assert not flags & 0x100
            for i in range(count):
                q = pos + start + struct.unpack_from('<I', b, pos + header + i * 4)[0]
                length = struct.unpack_from('<H', b, q)[0]
                strings.append(bytes(b[q+2:q+2+length*2]).decode('utf-16le'))
        if kind == 0x102 and strings[struct.unpack_from('<I', b, pos+20)[0]] == 'application':
            offset, width, count = struct.unpack_from('<HHH', b, pos+24)
            for i in range(count):
                q = pos + 16 + offset + i * width
                if strings[struct.unpack_from('<I', b, q+4)[0]] == 'debuggable':
                    assert b[q+15] == 0x12, 'Expected boolean debuggable attribute'
                    struct.pack_into('<I', b, q+8, 0xffffffff)
                    struct.pack_into('<I', b, q+16, 0xffffffff if enabled else 0)
                    changed += 1
        pos += size
    assert changed == 1, 'Expected one application debuggable attribute'
    return bytes(b)
