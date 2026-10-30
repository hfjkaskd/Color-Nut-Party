Color Nut Party Android signing backup

Alias: cnp_qt_qianmingwenjian
Keystore type: JKS
Key: RSA 2048 / SHA256withRSA
Certificate subject: Color Nut Party
Country: omitted

cnp_qt_qianmingwenjian.keystore.aesgcm is an AES-256-GCM encrypted
backup of the signing keystore. It cannot be passed directly to Unity.
The certificate PEM and signing-info.json contain public metadata only.

Restore using Python 3 with the cryptography package installed:

python restore_keystore.py --unlock-key /private/path/backup-unlock.key --output /private/path/cnp_qt_qianmingwenjian.keystore

The output directory must already exist. Existing output files are never
overwritten. Authentication and SHA-256 are verified before writing.

The backup unlock key is separate from the keystore and key passwords.
Keep the unlock key and signing passwords outside this repository.
Both the unlock key and passwords are necessary to recover and use the key.

Unity: use the restored .keystore, the alias above, and the separately
stored keystore/key passwords in Android Publishing Settings.
