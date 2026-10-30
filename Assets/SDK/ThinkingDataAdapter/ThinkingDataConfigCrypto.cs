using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ThinkingDataIntegration
{
    [Serializable]
#if UNITY_EDITOR
    public sealed class ThinkingDataConfig
#else
    internal sealed class ThinkingDataConfig
#endif
    {
        internal const int CurrentVersion = 2;
        internal const int LegacyVersionWithoutAppCode = 1;

        [SerializeField]
        private int version = CurrentVersion;

        [SerializeField]
        private string appCode = string.Empty;

        [SerializeField]
        private string appId = string.Empty;

        [SerializeField]
        private string serverUrl = string.Empty;

        internal int Version
        {
            get => version;
            set => version = value;
        }

        public string AppCode
        {
            get => appCode ?? string.Empty;
            set => appCode = value ?? string.Empty;
        }

        public string AppId
        {
            get => appId ?? string.Empty;
            set => appId = value ?? string.Empty;
        }

        public string ServerUrl
        {
            get => serverUrl ?? string.Empty;
            set => serverUrl = value ?? string.Empty;
        }

        public void Normalize()
        {
            AppCode = AppCode.Trim();
            AppId = AppId.Trim();
            ServerUrl = ServerUrl.Trim();
        }

        public bool TryValidate(out string error)
        {
            return TryValidate(true, out error);
        }

        internal bool TryValidateLegacy(out string error)
        {
            return TryValidate(false, out error);
        }

        private bool TryValidate(bool requireAppCode, out string error)
        {
            Normalize();
            error = string.Empty;

            if (requireAppCode && string.IsNullOrEmpty(AppCode))
            {
                error = "ThinkingData App Code is required.";
                return false;
            }

            if (string.IsNullOrEmpty(AppId) || string.IsNullOrEmpty(ServerUrl))
            {
                error = "ThinkingData App ID and Server URL are both required.";
                return false;
            }

            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
                (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                error = "ThinkingData Server URL must be an absolute HTTP or HTTPS URL.";
                return false;
            }

            return true;
        }
    }

#if UNITY_EDITOR
    public static class ThinkingDataConfigCrypto
#else
    internal static class ThinkingDataConfigCrypto
#endif
    {
        public const string ResourcePath = "ThinkingData/thinking_data_config";
        public const string AssetPath =
            "Assets/Resources/ThinkingData/thinking_data_config.bytes";

        private const byte EnvelopeVersion = 1;
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int HmacLength = 32;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TDCFG001");

        // Client-side encryption is obfuscation. Splitting the seed only avoids
        // storing the derived key as one contiguous constant in the assembly.
        private static readonly byte[] SeedPartA =
        {
            0xA7, 0x31, 0x5C, 0xE2, 0x19, 0xD4, 0x68, 0x0F,
            0xB3, 0x7D, 0x42, 0x96, 0xEC, 0x25, 0x81, 0x5A,
            0x14, 0xC9, 0x73, 0xBE, 0x06, 0xF1, 0x4D, 0x88,
            0xD2, 0x3B, 0xAF, 0x60, 0x97, 0x0C, 0xE5, 0x51
        };

        private static readonly byte[] SeedPartB =
        {
            0x3E, 0xC4, 0x91, 0x0B, 0xF6, 0x52, 0xAD, 0x78,
            0x1C, 0xE9, 0x35, 0xD0, 0x47, 0xBA, 0x63, 0x9F,
            0xE1, 0x26, 0x8B, 0x54, 0xCD, 0x39, 0xF7, 0x12,
            0x6A, 0x95, 0x08, 0xDE, 0x41, 0xB8, 0x27, 0xFC
        };

        internal static bool TryLoadFromResources(
            out ThinkingDataConfig config,
            out string error)
        {
            config = null;
            error = string.Empty;

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                error =
                    $"Encrypted ThinkingData config was not found at Resources/{ResourcePath}.bytes.";
                return false;
            }

            if (asset.bytes == null || asset.bytes.Length == 0)
            {
                error = "Encrypted ThinkingData config is empty.";
                return false;
            }

            return TryDecrypt(asset.bytes, out config, out error);
        }

        public static bool TryDecrypt(
            byte[] encryptedBytes,
            out ThinkingDataConfig config,
            out string error)
        {
            config = null;
            error = string.Empty;

            try
            {
                if (encryptedBytes == null || encryptedBytes.Length == 0)
                {
                    throw new InvalidDataException(
                        "Encrypted ThinkingData config is empty.");
                }

                var headerLength = Magic.Length + 1 + SaltLength + IvLength;
                if (encryptedBytes.Length < headerLength + IvLength + HmacLength)
                {
                    throw new InvalidDataException(
                        "Encrypted ThinkingData config is truncated.");
                }

                for (var index = 0; index < Magic.Length; index++)
                {
                    if (encryptedBytes[index] != Magic[index])
                    {
                        throw new InvalidDataException(
                            "Encrypted ThinkingData config header is invalid.");
                    }
                }

                var offset = Magic.Length;
                var version = encryptedBytes[offset++];
                if (version != EnvelopeVersion)
                {
                    throw new InvalidDataException(
                        $"Encrypted ThinkingData config version {version} is not supported.");
                }

                var salt = CopyRange(encryptedBytes, offset, SaltLength);
                offset += SaltLength;
                var iv = CopyRange(encryptedBytes, offset, IvLength);
                offset += IvLength;

                var cipherLength = encryptedBytes.Length - offset - HmacLength;
                if (cipherLength <= 0 || cipherLength % IvLength != 0)
                {
                    throw new InvalidDataException(
                        "Encrypted ThinkingData config payload length is invalid.");
                }

                var cipherBytes = CopyRange(encryptedBytes, offset, cipherLength);
                var actualHmac = CopyRange(
                    encryptedBytes,
                    offset + cipherLength,
                    HmacLength);
                var signedBytes = CopyRange(
                    encryptedBytes,
                    0,
                    offset + cipherLength);

                DeriveKeys(salt, out var encryptionKey, out var hmacKey);
                try
                {
                    byte[] expectedHmac;
                    using (var hmac = new HMACSHA256(hmacKey))
                    {
                        expectedHmac = hmac.ComputeHash(signedBytes);
                    }

                    if (!FixedTimeEquals(expectedHmac, actualHmac))
                    {
                        throw new CryptographicException(
                            "Encrypted ThinkingData config integrity validation failed.");
                    }

                    byte[] plainBytes;
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = 256;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = encryptionKey;
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor())
                        {
                            plainBytes = decryptor.TransformFinalBlock(
                                cipherBytes,
                                0,
                                cipherBytes.Length);
                        }
                    }

                    var json = Encoding.UTF8.GetString(plainBytes);
                    config = JsonUtility.FromJson<ThinkingDataConfig>(json);
                    if (config == null)
                    {
                        throw new InvalidDataException(
                            "Decrypted ThinkingData config JSON is invalid.");
                    }

                    if (config.Version != ThinkingDataConfig.CurrentVersion &&
                        config.Version != ThinkingDataConfig.LegacyVersionWithoutAppCode)
                    {
                        throw new InvalidDataException(
                            $"ThinkingData config schema version {config.Version} is not supported.");
                    }

                    var isLegacyConfig =
                        config.Version ==
                        ThinkingDataConfig.LegacyVersionWithoutAppCode;
                    var isValid = isLegacyConfig
                        ? config.TryValidateLegacy(out error)
                        : config.TryValidate(out error);
                    if (!isValid)
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
        public static byte[] Encrypt(ThinkingDataConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Version = ThinkingDataConfig.CurrentVersion;
            if (!config.TryValidate(out var error))
            {
                throw new InvalidDataException(error);
            }

            return EncryptJson(JsonUtility.ToJson(config));
        }

        internal static byte[] EncryptJsonForTests(string json)
        {
            return EncryptJson(json);
        }

        private static byte[] EncryptJson(string json)
        {
            var salt = new byte[SaltLength];
            var iv = new byte[IvLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
                random.GetBytes(iv);
            }

            var plainBytes = Encoding.UTF8.GetBytes(json);
            DeriveKeys(salt, out var encryptionKey, out var hmacKey);
            try
            {
                byte[] cipherBytes;
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;

                    using (var encryptor = aes.CreateEncryptor())
                    {
                        cipherBytes = encryptor.TransformFinalBlock(
                            plainBytes,
                            0,
                            plainBytes.Length);
                    }
                }

                byte[] signedBytes;
                using (var output = new MemoryStream())
                {
                    output.Write(Magic, 0, Magic.Length);
                    output.WriteByte(EnvelopeVersion);
                    output.Write(salt, 0, salt.Length);
                    output.Write(iv, 0, iv.Length);
                    output.Write(cipherBytes, 0, cipherBytes.Length);
                    signedBytes = output.ToArray();
                }

                byte[] signature;
                using (var hmac = new HMACSHA256(hmacKey))
                {
                    signature = hmac.ComputeHash(signedBytes);
                }

                var result = new byte[signedBytes.Length + signature.Length];
                Buffer.BlockCopy(signedBytes, 0, result, 0, signedBytes.Length);
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
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
                Array.Clear(hmacKey, 0, hmacKey.Length);
            }
        }
#endif

        private static void DeriveKeys(
            byte[] salt,
            out byte[] encryptionKey,
            out byte[] hmacKey)
        {
            var master = new byte[SeedPartA.Length];
            for (var index = 0; index < master.Length; index++)
            {
                master[index] = (byte)(SeedPartA[index] ^ SeedPartB[index]);
            }

            try
            {
                encryptionKey = DeriveKey(
                    master,
                    salt,
                    "THINKINGDATA-CONFIG-ENCRYPTION");
                hmacKey = DeriveKey(
                    master,
                    salt,
                    "THINKINGDATA-CONFIG-INTEGRITY");
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
            var purposeBytes = Encoding.UTF8.GetBytes(purpose);
            var input = new byte[purposeBytes.Length + salt.Length];
            Buffer.BlockCopy(purposeBytes, 0, input, 0, purposeBytes.Length);
            Buffer.BlockCopy(salt, 0, input, purposeBytes.Length, salt.Length);

            using (var hmac = new HMACSHA256(master))
            {
                return hmac.ComputeHash(input);
            }
        }

        private static byte[] CopyRange(byte[] source, int offset, int length)
        {
            var result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }
}
