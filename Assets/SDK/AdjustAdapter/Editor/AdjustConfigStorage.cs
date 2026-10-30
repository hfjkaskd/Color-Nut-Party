using System;
using System.IO;

namespace AdjustIntegration.Editor
{
    internal enum AdjustConfigLoadStatus
    {
        MissingOrEmpty,
        Loaded,
        Invalid
    }

    internal enum AdjustConfigWriteStatus
    {
        Saved,
        Deleted,
        Rejected
    }

    internal static class AdjustConfigStorage
    {
        internal static AdjustConfigLoadStatus Load(
            string fullPath,
            out AdjustIntegrationConfig config,
            out string error)
        {
            config = new AdjustIntegrationConfig();
            error = string.Empty;

            if (!File.Exists(fullPath))
            {
                return AdjustConfigLoadStatus.MissingOrEmpty;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                if (bytes.Length == 0)
                {
                    return AdjustConfigLoadStatus.MissingOrEmpty;
                }

                if (!AdjustConfigCrypto.TryDecrypt(
                        bytes,
                        out config,
                        out error))
                {
                    config = new AdjustIntegrationConfig();
                    return AdjustConfigLoadStatus.Invalid;
                }

                return AdjustConfigLoadStatus.Loaded;
            }
            catch (Exception exception)
            {
                config = new AdjustIntegrationConfig();
                error = exception.Message;
                return AdjustConfigLoadStatus.Invalid;
            }
        }

        internal static AdjustConfigWriteStatus Apply(
            string fullPath,
            AdjustIntegrationConfig config,
            out string error)
        {
            error = string.Empty;
            config = config ?? new AdjustIntegrationConfig();
            config.Normalize();

            bool hasAnyValue =
                !string.IsNullOrEmpty(config.AppToken) ||
                !string.IsNullOrEmpty(config.Ipu3EventToken) ||
                !string.IsNullOrEmpty(config.Ipu5EventToken) ||
                !string.IsNullOrEmpty(config.Ipu10EventToken);

            if (!hasAnyValue)
            {
                try
                {
                    Delete(fullPath);
                    return AdjustConfigWriteStatus.Deleted;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return AdjustConfigWriteStatus.Rejected;
                }
            }

            if (!config.TryValidate(out error))
            {
                return AdjustConfigWriteStatus.Rejected;
            }

            try
            {
                byte[] encryptedBytes =
                    AdjustConfigCrypto.Encrypt(config);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteAtomically(fullPath, encryptedBytes);
                return AdjustConfigWriteStatus.Saved;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return AdjustConfigWriteStatus.Rejected;
            }
        }

        private static void Delete(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            string metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static void WriteAtomically(
            string fullPath,
            byte[] bytes)
        {
            string temporaryPath = fullPath + ".tmp";
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
