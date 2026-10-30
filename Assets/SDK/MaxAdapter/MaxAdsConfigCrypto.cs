using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AppLovinMax.ThirdParty.MiniJson;
using UnityEngine;

namespace MaxAdsIntegration
{
    public sealed class MaxAdsPlatformConfig
    {
        public string RewardedAdUnitId { get; set; } = string.Empty;
        public string InterstitialAdUnitId { get; set; } = string.Empty;

        internal void Normalize()
        {
            RewardedAdUnitId = (RewardedAdUnitId ?? string.Empty).Trim();
            InterstitialAdUnitId = (InterstitialAdUnitId ?? string.Empty).Trim();
        }
    }

    public sealed class MaxAdsConfig
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public MaxAdsPlatformConfig Android { get; set; } = new MaxAdsPlatformConfig();
        public MaxAdsPlatformConfig Ios { get; set; } = new MaxAdsPlatformConfig();

        public MaxAdsPlatformConfig GetPlatformConfig(bool useIos)
        {
            EnsureDefaults();
            return useIos ? Ios : Android;
        }

        internal void EnsureDefaults()
        {
            Android = Android ?? new MaxAdsPlatformConfig();
            Ios = Ios ?? new MaxAdsPlatformConfig();
            Android.Normalize();
            Ios.Normalize();
        }
    }

    public static class MaxAdsConfigCrypto
    {
        public const string ResourcePath = "MaxAdapter/max_ads_config";
        public const string AssetPath = "Assets/Resources/MaxAdapter/max_ads_config.bytes";

        private const byte EnvelopeVersion = 1;
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int HmacLength = 32;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MAXCFG01");

        // The two halves are combined only while deriving the per-file keys.
        // Client-side encryption is obfuscation, not a substitute for server-side secrets.
        private static readonly byte[] SeedPartA =
        {
            0x61, 0x92, 0x2D, 0xCF, 0x0A, 0x74, 0xE3, 0x18,
            0xB5, 0x49, 0x87, 0xF1, 0x36, 0xAD, 0x5C, 0x02,
            0xD8, 0x23, 0x9E, 0x45, 0x7B, 0xC0, 0x14, 0xFA,
            0x58, 0x6F, 0xB9, 0x31, 0xE6, 0x0D, 0xA2, 0x77
        };

        private static readonly byte[] SeedPartB =
        {
            0x17, 0xEB, 0x84, 0x39, 0xD6, 0x21, 0x5A, 0xC7,
            0x03, 0xBD, 0x68, 0x9F, 0x42, 0xF0, 0x11, 0xAC,
            0x6E, 0x95, 0x27, 0xDB, 0x30, 0x7C, 0xE9, 0x54,
            0x82, 0x1A, 0xC4, 0x63, 0x0F, 0xB7, 0x48, 0xD1
        };

        public static bool TryLoadFromResources(out MaxAdsConfig config, out string error)
        {
            config = null;
            error = string.Empty;

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                error = $"Encrypted MAX config was not found at Resources/{ResourcePath}.bytes.";
                return false;
            }

            return TryDecrypt(asset.bytes, out config, out error);
        }

        public static bool TryDecrypt(byte[] encryptedBytes, out MaxAdsConfig config, out string error)
        {
            config = null;
            error = string.Empty;

            try
            {
                if (encryptedBytes == null)
                {
                    throw new InvalidDataException("Encrypted MAX config is null.");
                }

                var headerLength = Magic.Length + 1 + SaltLength + IvLength;
                if (encryptedBytes.Length < headerLength + HmacLength + IvLength)
                {
                    throw new InvalidDataException("Encrypted MAX config is truncated.");
                }

                for (var index = 0; index < Magic.Length; index++)
                {
                    if (encryptedBytes[index] != Magic[index])
                    {
                        throw new InvalidDataException("Encrypted MAX config header is invalid.");
                    }
                }

                var offset = Magic.Length;
                var envelopeVersion = encryptedBytes[offset++];
                if (envelopeVersion != EnvelopeVersion)
                {
                    throw new InvalidDataException(
                        $"Encrypted MAX config version {envelopeVersion} is not supported.");
                }

                var salt = CopyRange(encryptedBytes, offset, SaltLength);
                offset += SaltLength;
                var iv = CopyRange(encryptedBytes, offset, IvLength);
                offset += IvLength;

                var cipherLength = encryptedBytes.Length - offset - HmacLength;
                if (cipherLength <= 0 || cipherLength % IvLength != 0)
                {
                    throw new InvalidDataException("Encrypted MAX config payload length is invalid.");
                }

                var cipherBytes = CopyRange(encryptedBytes, offset, cipherLength);
                var actualHmac = CopyRange(encryptedBytes, offset + cipherLength, HmacLength);
                var signedBytes = CopyRange(encryptedBytes, 0, offset + cipherLength);

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
                            "Encrypted MAX config integrity validation failed.");
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
                    config = Deserialize(json);
                    config.EnsureDefaults();
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
        public static byte[] Encrypt(MaxAdsConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Version = MaxAdsConfig.CurrentVersion;
            config.EnsureDefaults();

            var salt = new byte[SaltLength];
            var iv = new byte[IvLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
                random.GetBytes(iv);
            }

            var plainBytes = Encoding.UTF8.GetBytes(Serialize(config));
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
                Buffer.BlockCopy(signature, 0, result, signedBytes.Length, signature.Length);
                return result;
            }
            finally
            {
                Array.Clear(encryptionKey, 0, encryptionKey.Length);
                Array.Clear(hmacKey, 0, hmacKey.Length);
            }
        }
#endif

        private static string Serialize(MaxAdsConfig config)
        {
            var root = new Dictionary<string, object>
            {
                ["version"] = config.Version,
                ["android"] = SerializePlatform(config.Android),
                ["ios"] = SerializePlatform(config.Ios)
            };

            return Json.Serialize(root);
        }

        private static Dictionary<string, object> SerializePlatform(MaxAdsPlatformConfig platform)
        {
            return new Dictionary<string, object>
            {
                ["rewardedAdUnitId"] = platform.RewardedAdUnitId,
                ["interstitialAdUnitId"] = platform.InterstitialAdUnitId
            };
        }

        private static MaxAdsConfig Deserialize(string json)
        {
            var root = Json.Deserialize(json) as IDictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("Decrypted MAX config JSON is invalid.");
            }

            var version = ReadInt(root, "version");
            if (version != MaxAdsConfig.CurrentVersion)
            {
                throw new InvalidDataException(
                    $"MAX config schema version {version} is not supported.");
            }

            return new MaxAdsConfig
            {
                Version = version,
                Android = ReadPlatform(root, "android"),
                Ios = ReadPlatform(root, "ios")
            };
        }

        private static MaxAdsPlatformConfig ReadPlatform(
            IDictionary<string, object> root,
            string key)
        {
            if (!root.TryGetValue(key, out var value) ||
                !(value is IDictionary<string, object> platform))
            {
                return new MaxAdsPlatformConfig();
            }

            return new MaxAdsPlatformConfig
            {
                RewardedAdUnitId = ReadString(platform, "rewardedAdUnitId"),
                InterstitialAdUnitId = ReadString(platform, "interstitialAdUnitId")
            };
        }

        private static int ReadInt(IDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static string ReadString(IDictionary<string, object> data, string key)
        {
            return data.TryGetValue(key, out var value) && value != null
                ? value.ToString()
                : string.Empty;
        }

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
                encryptionKey = DeriveKey(master, salt, "MAX-CONFIG-ENCRYPTION");
                hmacKey = DeriveKey(master, salt, "MAX-CONFIG-INTEGRITY");
            }
            finally
            {
                Array.Clear(master, 0, master.Length);
            }
        }

        private static byte[] DeriveKey(byte[] master, byte[] salt, string purpose)
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
