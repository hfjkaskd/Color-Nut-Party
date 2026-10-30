"""Restore the encrypted signing backup using its separately stored unlock key."""
import argparse
import base64
import hashlib
import json
from pathlib import Path

from cryptography.hazmat.primitives.ciphers.aead import AESGCM


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unlock-key', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    backup = Path(__file__).resolve().parent / 'cnp_qt_qianmingwenjian.keystore.aesgcm'
    bundle = json.loads(backup.read_text(encoding='utf-8'))
    if bundle['format'] != 'ColorNutParty.AndroidSigning.v1' or bundle['algorithm'] != 'AES-256-GCM':
        raise ValueError('Unsupported signing backup format')
    key = base64.b64decode(args.unlock_key.read_text(encoding='ascii').strip(), validate=True)
    plaintext = AESGCM(key).decrypt(
        base64.b64decode(bundle['nonce'], validate=True),
        base64.b64decode(bundle['ciphertext'], validate=True),
        bundle['format'].encode('ascii'),
    )
    if hashlib.sha256(plaintext).hexdigest() != bundle['plaintext_sha256']:
        raise ValueError('Signing backup checksum mismatch')
    # Exclusive creation prevents replacing an existing signing key.
    with args.output.open('xb') as stream:
        stream.write(plaintext)
    print('Signing keystore restored and checksum verified:', args.output)


if __name__ == '__main__':
    main()
