using System;
using UnityEngine;

/// <summary>
/// Platform device-information helpers used by the SDK integration layer.
/// </summary>
public static class DeviceInfo
{
    private const string GeneratedDeviceIdPlayerPrefsKey =
        "SDKAdapter.GeneratedCid";
    private const int SimStateUnknown = 0;
    private const int SimStatePresent = 1;
    private const int SimStateAbsent = 2;

    public static string GetOrCreateCid()
    {
        var deviceId = SystemInfo.deviceUniqueIdentifier;
        if (!string.IsNullOrWhiteSpace(deviceId) &&
            !string.Equals(
                deviceId,
                "n/a",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                deviceId,
                "unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return deviceId;
        }

        deviceId = PlayerPrefs.GetString(
            GeneratedDeviceIdPlayerPrefsKey,
            string.Empty);
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId;
        }

        deviceId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(GeneratedDeviceIdPlayerPrefsKey, deviceId);
        PlayerPrefs.Save();
        return deviceId;
    }

    /// <summary>
    /// Returns 0 when unknown, 1 when a SIM is present, and 2 when absent.
    /// </summary>
    public static int GetSimState()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            using (var activity =
                   unityPlayer.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (var telephonyManager =
                   activity.Call<AndroidJavaObject>(
                       "getSystemService",
                       "phone"))
            {
                if (telephonyManager == null)
                {
                    return SimStateUnknown;
                }

                var nativeSimState =
                    telephonyManager.Call<int>("getSimState");
                const int nativeSimStateUnknown = 0;
                const int nativeSimStateAbsent = 1;
                if (nativeSimState == nativeSimStateUnknown)
                {
                    return SimStateUnknown;
                }

                return nativeSimState == nativeSimStateAbsent
                    ? SimStateAbsent
                    : SimStatePresent;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[DeviceInfo] SIM state request failed (" +
                exception.GetType().Name + ").");
            return SimStateUnknown;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // Intentionally left unimplemented for iOS.
        return SimStateUnknown;
#else
        return SimStateUnknown;
#endif
    }

    public static bool GetIsVpnUser()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            using (var activity =
                   unityPlayer.GetStatic<AndroidJavaObject>(
                       "currentActivity"))
            using (var connectivityManager =
                   activity.Call<AndroidJavaObject>(
                       "getSystemService",
                       "connectivity"))
            using (var activeNetwork =
                   connectivityManager.Call<AndroidJavaObject>(
                       "getActiveNetwork"))
            using (var capabilities = activeNetwork == null
                       ? null
                       : connectivityManager.Call<AndroidJavaObject>(
                           "getNetworkCapabilities",
                           activeNetwork))
            {
                const int transportVpn = 4;
                return capabilities != null &&
                       capabilities.Call<bool>(
                           "hasTransport",
                           transportVpn);
            }
        }
        catch (Exception)
        {
            return false;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// Gets the Android GAID. Other platforms currently return empty.
    /// </summary>
    public static bool TryRequestAdvertisingId(Action<string> callback)
    {
        if (callback == null)
        {
            Debug.LogWarning(
                "[DeviceInfo] Advertising ID callback is null.");
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AdjustSdk.Adjust.GetGoogleAdId(
                value => callback(NormalizeAdvertisingId(value)));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[DeviceInfo] GAID request failed (" +
                exception.GetType().Name + ").");
            callback(string.Empty);
            return false;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // Intentionally left unimplemented for iOS.
        callback(string.Empty);
        return false;
#else
        callback(string.Empty);
        return false;
#endif
    }

    private static string NormalizeAdvertisingId(string advertisingId)
    {
        var normalized = (advertisingId ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        foreach (var character in normalized)
        {
            if (character != '0' && character != '-')
            {
                return normalized;
            }
        }

        return string.Empty;
    }
}
