#!/usr/bin/env python3
"""
decrypt_assets.py -- decrypt AI: The Somnium Files Unity asset Lua modules.

The game stores all Lua bytecode in Unity TextAssets as .bytes files, encrypted
with a trivial counter-XOR cipher:
    plaintext[i] = encrypted[i] XOR (i & 0xFF)   for i >= 4
The first 4 bytes (\x1bLua) are stored as plaintext (no XOR applied).

Usage:
    python3 decrypt_assets.py <input.bytes> [output.luac]
    python3 decrypt_assets.py --all <input_dir> <output_dir>

Without --all, decrypts a single file and writes to output.luac (or
<input>.luac if omitted).  With --all, recurses input_dir and decrypts every
.bytes file (skipping .meta files), mirroring the directory structure under
output_dir.
"""

import sys
import os


def decrypt(data: bytes) -> bytes:
    if len(data) < 5:
        raise ValueError("File too short to be a valid encrypted Lua module")
    out = bytearray(data)
    for i in range(4, len(out)):
        out[i] ^= i & 0xFF
    if out[:4] != b'\x1bLua':
        raise ValueError("Magic mismatch after decryption -- file is not an encrypted Lua module")
    if out[4] != 0x51:
        raise ValueError(f"Unexpected Lua version byte 0x{out[4]:02x} (expected 0x51 for Lua 5.1)")
    return bytes(out)


def decrypt_file(src: str, dst: str) -> None:
    raw = open(src, 'rb').read()
    dec = decrypt(raw)
    os.makedirs(os.path.dirname(dst) or '.', exist_ok=True)
    open(dst, 'wb').write(dec)
    print(f"  {src} -> {dst}  ({len(raw)} bytes)")


def decrypt_all(src_dir: str, dst_dir: str) -> None:
    count = 0
    for root, dirs, files in os.walk(src_dir):
        dirs.sort()
        for fn in sorted(files):
            if not fn.endswith('.bytes') or fn.endswith('.meta'):
                continue
            src = os.path.join(root, fn)
            rel = os.path.relpath(src, src_dir)
            dst = os.path.join(dst_dir, rel[:-len('.bytes')] + '.luac')
            try:
                decrypt_file(src, dst)
                count += 1
            except Exception as e:
                print(f"  SKIP {src}: {e}")
    print(f"\nDecrypted {count} file(s) -> {dst_dir}")


if __name__ == '__main__':
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        sys.exit(0)

    if args[0] == '--all':
        if len(args) < 3:
            print("Usage: decrypt_assets.py --all <input_dir> <output_dir>")
            sys.exit(1)
        decrypt_all(args[1], args[2])
    else:
        src = args[0]
        dst = args[1] if len(args) > 1 else src.removesuffix('.bytes') + '.luac'
        decrypt_file(src, dst)
