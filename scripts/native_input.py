"""Exact-source native input patch; preserve every original ELF section and DEX."""
import hashlib
import struct

NATIVE_SHA='eb8ab8714c7eda4dbf49c5f032557caba08c4eadfe02742b8337c7f184cf4724'
NATIVE_MEMBER='assets/libs/armeabi-v7a/libunity.so'
CALL_SITES=(0x2e4048,0x2e4178)
TRAMPOLINE=struct.pack('<8I',0xe92d4008,0xe5953000,0xe12fff33,0xe3052002,
                       0xe1500002,0x03010002,0xe8bd4008,0xe12fff1e)


def sha(data):return hashlib.sha256(data).hexdigest()
def aligned(value):return (value+4095)&~4095


def patch_native(original):
    if sha(original)!=NATIVE_SHA:raise ValueError('Unknown or already patched native library')
    if original[:7]!=b'\x7fELF\x01\x01\x01':raise ValueError('Expected original little-endian ELF32')
    phoff=struct.unpack_from('<I',original,28)[0]
    entsize,count=struct.unpack_from('<HH',original,42)
    if entsize!=32 or count!=5:raise ValueError('Unexpected original program headers')
    raw_headers=original[phoff:phoff+entsize*count]
    headers=[struct.unpack_from('<8I',raw_headers,i*32) for i in range(count)]
    loads=[h for h in headers if h[0]==1]
    if len(loads)!=2 or loads[0][1:3]!=(0,0):raise ValueError('Unexpected original load layout')
    rx,rw=loads
    file_offset=rx[1]+rx[4];code_address=rx[2]+rx[5]
    shift=32768
    if (file_offset!=rw[1] or rw[2]-code_address<32 or rx[4]!=rx[5]
            or rx[6]!=5 or rw[6]!=6 or rx[7]!=shift or rw[7]!=shift):
        raise ValueError('Missing original RX/RW virtual gap')
    shoff=struct.unpack_from('<I',original,32)[0]
    shentsize,shcount=struct.unpack_from('<HH',original,46)
    if shentsize!=40 or shoff<file_offset:raise ValueError('Unexpected original section table')
    result=bytearray(original[:file_offset]+TRAMPOLINE+bytes(shift-len(TRAMPOLINE))+original[file_offset:])
    struct.pack_into('<I',result,32,shoff+shift)
    for i,header in enumerate(headers):
        header=list(header)
        if header==list(rx):header[4]+=len(TRAMPOLINE);header[5]+=len(TRAMPOLINE)
        elif header[4] and header[1]>=file_offset:header[1]+=shift
        struct.pack_into('<8I',result,phoff+i*32,*header)
    for i in range(shcount):
        entry=shoff+i*shentsize
        offset=struct.unpack_from('<I',original,entry+16)[0]
        if offset>=file_offset:struct.pack_into('<I',result,entry+shift+16,offset+shift)
    for site in CALL_SITES:
        if original[site-4:site+4]!=bytes.fromhex('0fe0a0e100f095e5'):
            raise ValueError('Missing original getSource indirect-call boundary')
        delta=code_address-site-8
        if delta%4 or not -(1<<25)<=delta<(1<<25):raise ValueError('ARM branch range exceeded')
        struct.pack_into('<I',result,site,0xea000000|((delta>>2)&0xffffff))
    meta=dict(original_native_sha256=NATIVE_SHA,candidate_native_sha256=sha(result),
              call_sites=list(CALL_SITES),trampoline_address=code_address,
              trampoline_hex=TRAMPOLINE.hex(),program_header_offset=phoff,
              layout='extend_original_rx_in_virtual_gap',inserted_file_offset=file_offset,
              inserted_file_size=shift,source_from='0x5002',source_to='0x1002')

    return bytes(result),meta

