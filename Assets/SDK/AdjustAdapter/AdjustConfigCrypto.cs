using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AdjustIntegration
{
    [Serializable]
#if UNITY_EDITOR
    public sealed class AdjustIntegrationConfig
#else
    internal sealed class AdjustIntegrationConfig
#endif
    {
        internal const int CurrentVersion = 1;

        [SerializeField]
        private int version = CurrentVersion;

        [SerializeField]
        private string appToken = string.Empty;

        [SerializeField]
        private string ipu3EventToken = string.Empty;

        [SerializeField]
        private string ipu5EventToken = string.Empty;

        [SerializeField]
        private string ipu10EventToken = string.Empty;

        internal int Version
        {
            get { return version; }
            set { version = value; }
        }

        public string AppToken
        {
            get { return appToken ?? string.Empty; }
            set { appToken = value ?? string.Empty; }
        }

        public string Ipu3EventToken
        {
            get { return ipu3EventToken ?? string.Empty; }
            set { ipu3EventToken = value ?? string.Empty; }
        }

        public string Ipu5EventToken
        {
            get { return ipu5EventToken ?? string.Empty; }
            set { ipu5EventToken = value ?? string.Empty; }
        }

        public string Ipu10EventToken
        {
            get { return ipu10EventToken ?? string.Empty; }
            set { ipu10EventToken = value ?? string.Empty; }
        }

        public void Normalize()
        {
            AppToken = AppToken.Trim();
            Ipu3EventToken = Ipu3EventToken.Trim();
            Ipu5EventToken = Ipu5EventToken.Trim();
            Ipu10EventToken = Ipu10EventToken.Trim();
        }

        public bool TryValidate(out string error)
        {
            Normalize();
            error = string.Empty;

            if (string.IsNullOrEmpty(AppToken))
            {
                error = "Adjust app token is required.";
                return false;
            }

            if (string.IsNullOrEmpty(Ipu3EventToken) ||
                string.IsNullOrEmpty(Ipu5EventToken) ||
                string.IsNullOrEmpty(Ipu10EventToken))
            {
                error = "Adjust IPU 3, 5, and 10 event tokens are all required.";
                return false;
            }

            return true;
        }

        internal string GetIpuEventToken(int impressionCount)
        {
            switch (impressionCount)
            {
                case 3:
                    return Ipu3EventToken;

                case 5:
                    return Ipu5EventToken;

                case 10:
                    return Ipu10EventToken;

                default:
                    return string.Empty;
            }
        }
    }

#if UNITY_EDITOR
    public static class AdjustConfigCrypto
#else
    internal static class AdjustConfigCrypto
#endif
    {
        public const string ResourcePath = "Adjust/adjust_config";
        public const string AssetPath =
            "Assets/Resources/Adjust/adjust_config.bytes";

        private const byte EnvelopeVersion = 1;
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int HmacLength = 32;

        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("ADJCFG01");

        // This only obfuscates client-side configuration. The two seed halves
        // avoid storing a derived encryption key as one contiguous constant.
        private static readonly byte[] SeedPartA =
        {
            0x9B, 0x41, 0xE6, 0x17, 0x72, 0xC8, 0x35, 0xAF,
            0x0D, 0xF3, 0x58, 0x94, 0x21, 0xBE, 0x67, 0xCA,
            0x33, 0x8D, 0xF0, 0x4A, 0x16, 0xDB, 0x75, 0x2C,
            0xE1, 0x69, 0x07, 0xB4, 0x5E, 0x92, 0xC3, 0x28
        };

        private static readonly byte[] SeedPartB =
        {
            0x24, 0xDE, 0x73, 0x8A, 0xCF, 0x15, 0xE2, 0x39,
            0xB7, 0x46, 0x9D, 0x0B, 0xF4, 0x51, 0xAC, 0x62,
            0xD8, 0x27, 0x5B, 0xE9, 0x80, 0x34, 0xC6, 0x11,
            0x7F, 0xA2, 0xDC, 0x48, 0x03, 0xED, 0x56, 0x99
        };

        internal static bool TryLoadFromResources(
            out AdjustIntegrationConfig config,
            out string error)
        {
            config = null;
            error = string.Empty;

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                error =
                    "Encrypted Adjust config was not found at Resources/" +
                    ResourcePath + ".bytes.";
                return false;
            }

            if (asset.bytes == null || asset.bytes.Length == 0)
            {
                error = "Encrypted Adjust config is empty.";
                return false;
            }

            return TryDecrypt(asset.bytes, out config, out error);
        }

        public static bool TryDecrypt(
            byte[] encryptedBytes,
            out AdjustIntegrationConfig config,
            out string error)
        {
            config = null;
            error = string.Empty;

            try
            {
                byte[] envelopeBytes = DecodeTransport(encryptedBytes);
                if (envelopeBytes == null || envelopeBytes.Length == 0)
                {
                    throw new InvalidDataException(
                        "Encrypted Adjust config is empty.");
                }

                int headerLength = Magic.Length + 1 + SaltLength + IvLength;
                if (envelopeBytes.Length <
                    headerLength + IvLength + HmacLength)
                {
                    throw new InvalidDataException(
                        "Encrypted Adjust config is truncated.");
                }

                for (int index = 0; index < Magic.Length; index++)
                {
                    if (envelopeBytes[index] != Magic[index])
                    {
                        throw new InvalidDataException(
                            "Encrypted Adjust config header is invalid.");
                    }
                }

                int offset = Magic.Length;
                byte envelopeVersion = envelopeBytes[offset++];
                if (envelopeVersion != EnvelopeVersion)
                {
                    throw new InvalidDataException(
                        "Encrypted Adjust config version " +
                        envelopeVersion + " is not supported.");
                }

                byte[] salt = CopyRange(envelopeBytes, offset, SaltLength);
                offset += SaltLength;
                byte[] iv = CopyRange(envelopeBytes, offset, IvLength);
                offset += IvLength;

                int cipherLength =
                    envelopeBytes.Length - offset - HmacLength;
                if (cipherLength <= 0 || cipherLength % IvLength != 0)
                {
                    throw new InvalidDataException(
                        "Encrypted Adjust config payload length is invalid.");
                }

                byte[] cipherBytes =
                    CopyRange(envelopeBytes, offset, cipherLength);
                byte[] actualHmac = CopyRange(
                    envelopeBytes,
                    offset + cipherLength,
                    HmacLength);
                byte[] signedBytes =
                    CopyRange(envelopeBytes, 0, offset + cipherLength);

                byte[] encryptionKey;
                byte[] hmacKey;
                DeriveKeys(salt, out encryptionKey, out hmacKey);
                try
                {
                    byte[] expectedHmac;
                    using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                    {
                        expectedHmac = hmac.ComputeHash(signedBytes);
                    }

                    if (!FixedTimeEquals(expectedHmac, actualHmac))
                    {
                        throw new CryptographicException(
                            "Encrypted Adjust config integrity validation failed.");
                    }

                    byte[] plainBytes;
                    using (Aes aes = Aes.Create())
                    {
                        aes.KeySize = 256;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = encryptionKey;
                        aes.IV = iv;

                        using (ICryptoTransform decryptor =
                               aes.CreateDecryptor())
                        {
                            plainBytes = decryptor.TransformFinalBlock(
                                cipherBytes,
                                0,
                                cipherBytes.Length);
                        }
                    }

                    string json = Encoding.UTF8.GetString(plainBytes);
                    config = JsonUtility.FromJson<AdjustIntegrationConfig>(
                        json);
                    Array.Clear(plainBytes, 0, plainBytes.Length);

                    if (config == null)
                    {
                        throw new InvalidDataException(
                            "Decrypted Adjust config JSON is invalid.");
                    }

                    if (config.Version !=
                        AdjustIntegrationConfig.CurrentVersion)
                    {
                        throw new InvalidDataException(
                            "Adjust config schema version " +
                            config.Version + " is not supported.");
                    }

                    if (!config.TryValidate(out error))
                    {
                        config = null;
                        return false;
                    }

                    return true;
                }
                finally
                {
                    Array.Clear(encryptionKey, 0, encryptionKey.Length);
                    Array.Clear(hmacKey, 0, hmacKey.Length);
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                config = null;
                return false;
            }
        }

#if UNITY_EDITOR
        public static byte[] Encrypt(AdjustIntegrationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            config.Version = AdjustIntegrationConfig.CurrentVersion;
            string validationError;
            if (!config.TryValidate(out validationError))
            {
                throw new InvalidDataException(validationError);
            }

            byte[] salt = new byte[SaltLength];
            byte[] iv = new byte[IvLength];
            using (RandomNumberGenerator random =
                   RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
                random.GetBytes(iv);
            }

            string json = JsonUtility.ToJson(config);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptionKey;
            byte[] hmacKey;
            DeriveKeys(salt, out encryptionKey, out hmacKey);
            try
            {
                byte[] cipherBytes;
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;

                    using (ICryptoTransform encryptor =
                           aes.CreateEncryptor())
                    {
                        cipherBytes = encryptor.TransformFinalBlock(
                            plainBytes,
                            0,
                            plainBytes.Length);
                    }
                }

                byte[] signedBytes;
                using (MemoryStream output = new MemoryStream())
                {
                    output.Write(Magic, 0, Magic.Length);
                    output.WriteByte(EnvelopeVersion);
                    output.Write(salt, 0, salt.Length);
                    output.Write(iv, 0, iv.Length);
                    output.Write(cipherBytes, 0, cipherBytes.Length);
                    signedBytes = output.ToArray();
                }

                byte[] signature;
                using (HMACSHA256 hmac = new HMACSHA256(hmacKey))
                {
                    signature = hmac.ComputeHash(signedBytes);
                }

                byte[] result =
                    new byte[signedBytes.Length + signature.Length];
                Buffer.BlockCopy(
                    signedBytes,
                    0,
                    result,
                    0,
                    signedBytes.Length);
                Buffer.BlockCopy(
                    signature,
                    0,
                    result,
                    signedBytes.Length,
                    signature.Length);
                return result;
            }
            finally
            {
                Array.Clear(plainBytes, 0, plainBytes.Length);
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
                Array.Clear(hmacKey, 0, hmacKey.Length);
            }
        }
#endif

        private static byte[] DecodeTransport(byte[] source)
        {
            if (source == null || source.Length == 0 || HasMagic(source))
            {
                return source;
            }

            // TextAsset.bytes normally contains the raw envelope. Base64 is
            // also accepted so encrypted resources can be reviewed as text.
            try
            {
                string base64 = Encoding.ASCII.GetString(source).Trim();
                byte[] decoded = Convert.FromBase64String(base64);
                return HasMagic(decoded) ? decoded : source;
            }
            catch (FormatException)
            {
                return source;
            }
        }

        private static bool HasMagic(byte[] bytes)
        {
            if (bytes == null || bytes.Length < Magic.Length)
            {
                return false;
            }

            for (int index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] != Magic[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void DeriveKeys(
            byte[] salt,
            out byte[] encryptionKey,
            out byte[] hmacKey)
        {
            byte[] master = new byte[SeedPartA.Length];
            for (int index = 0; index < master.Length; index++)
            {
                master[index] =
                    (byte)(SeedPartA[index] ^ SeedPartB[index]);
            }

            try
            {
                encryptionKey = DeriveKey(
                    master,
                    salt,
                    "ADJUST-CONFIG-ENCRYPTION");
                hmacKey = DeriveKey(
                    master,
                    salt,
                    "ADJUST-CONFIG-INTEGRITY");
            }
            finally
            {
                Array.Clear(master, 0, master.Length);
            }
        }

        private static byte[] DeriveKey(
            byte[] master,
            byte[] salt,
            string purpose)
        {
            byte[] purposeBytes = Encoding.UTF8.GetBytes(purpose);
            byte[] input = new byte[purposeBytes.Length + salt.Length];
            Buffer.BlockCopy(
                purposeBytes,
                0,
                input,
                0,
                purposeBytes.Length);
            Buffer.BlockCopy(
                salt,
                0,
                input,
                purposeBytes.Length,
                salt.Length);

            using (HMACSHA256 hmac = new HMACSHA256(master))
            {
                return hmac.ComputeHash(input);
            }
        }

        private static byte[] CopyRange(
            byte[] source,
            int offset,
            int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }
}
