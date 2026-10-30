using System;
using System.IO;

namespace ThinkingDataIntegration.Editor
{
    internal enum ThinkingDataConfigLoadStatus
    {
        MissingOrEmpty,
        Loaded,
        Invalid
    }

    internal enum ThinkingDataConfigWriteStatus
    {
        Saved,
        Deleted,
        Rejected
    }

    internal static class ThinkingDataConfigStorage
    {
        internal static ThinkingDataConfigLoadStatus Load(
            string fullPath,
            out ThinkingDataConfig config,
            out string error)
        {
            config = new ThinkingDataConfig();
            error = string.Empty;

            if (!File.Exists(fullPath))
            {
                return ThinkingDataConfigLoadStatus.MissingOrEmpty;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                if (bytes.Length == 0)
                {
                    return ThinkingDataConfigLoadStatus.MissingOrEmpty;
                }

                if (!ThinkingDataConfigCrypto.TryDecrypt(
                        bytes,
                        out config,
                        out error))
                {
                    config = new ThinkingDataConfig();
                    return ThinkingDataConfigLoadStatus.Invalid;
                }

                return ThinkingDataConfigLoadStatus.Loaded;
            }
            catch (Exception exception)
            {
                config = new ThinkingDataConfig();
                error = exception.Message;
                return ThinkingDataConfigLoadStatus.Invalid;
            }
        }

        internal static ThinkingDataConfigWriteStatus Apply(
            string fullPath,
            ThinkingDataConfig config,
            out string error)
        {
            error = string.Empty;
            config = config ?? new ThinkingDataConfig();
            config.Normalize();

            var hasAppCode = !string.IsNullOrEmpty(config.AppCode);
            var hasAppId = !string.IsNullOrEmpty(config.AppId);
            var hasServerUrl = !string.IsNullOrEmpty(config.ServerUrl);
            if (!hasAppCode && !hasAppId && !hasServerUrl)
            {
                try
                {
                    Delete(fullPath);
                    return ThinkingDataConfigWriteStatus.Deleted;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return ThinkingDataConfigWriteStatus.Rejected;
                }
            }

            if (!hasAppCode || !hasAppId || !hasServerUrl)
            {
                error =
                    "App Code, App ID and Server URL must either all be filled or all be empty.";
                return ThinkingDataConfigWriteStatus.Rejected;
            }

            if (!config.TryValidate(out error))
            {
                return ThinkingDataConfigWriteStatus.Rejected;
            }

            try
            {
                Save(fullPath, config);
                return ThinkingDataConfigWriteStatus.Saved;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return ThinkingDataConfigWriteStatus.Rejected;
            }
        }

        internal static void Save(
            string fullPath,
            ThinkingDataConfig config)
        {
            var encryptedBytes = ThinkingDataConfigCrypto.Encrypt(config);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            WriteAtomically(fullPath, encryptedBytes);
        }

        private static void Delete(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static void WriteAtomically(string fullPath, byte[] bytes)
        {
            var temporaryPath = fullPath + ".tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, bytes);
                if (!File.Exists(fullPath))
                {
                    File.Move(temporaryPath, fullPath);
                    return;
                }

                File.Replace(temporaryPath, fullPath, null);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
