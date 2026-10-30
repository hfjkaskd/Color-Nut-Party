using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;

internal sealed partial class BizzaAnalyticsRuntime
{
  private const string PublicIpEndpoint = "https://api64.ipify.org";
  private const int LoginIpTimeoutSeconds = 3;
  private bool _startupLoginPending;
  private string _startupLoginTimestamp;
  private Coroutine _loginIpCoroutine;
  private UnityWebRequest _loginIpRequest;

  private void BeginStartupLogin()
  {
    _startupLoginPending = true;
    _startupLoginTimestamp = DateTime.UtcNow.ToString("o");
    try
    {
      _loginIpCoroutine = _host.RunCoroutine(LookupLoginIp());
    }
    catch (Exception ex)
    {
      LogWarn("login_ip(start_failed) " + ex.Message);
    }
    if (_loginIpCoroutine == null) CompleteStartupLogin("unknown");
  }

  private IEnumerator LookupLoginIp()
  {
    // Let BeginStartupLogin retain the coroutine before any synchronous completion.
    yield return null;
    try
    {
      UnityWebRequestAsyncOperation operation = null;
      try
      {
        _loginIpRequest = new UnityWebRequest(PublicIpEndpoint, "GET");
        _loginIpRequest.timeout = LoginIpTimeoutSeconds;
        _loginIpRequest.downloadHandler = new DownloadHandlerBuffer();
        // This public lookup carries no analytics identifiers, payload or authorization.
        operation = _loginIpRequest.SendWebRequest();
      }
      catch (Exception ex)
      {
        LogWarn("login_ip(request_failed) " + ex.Message);
      }

      var ip = "unknown";
      if (operation != null)
      {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!operation.isDone)
        {
          if (_shutdown || _paused) yield break;
          if (timer.Elapsed.TotalSeconds >= LoginIpTimeoutSeconds)
          {
            _loginIpRequest.Abort();
            break;
          }
          yield return null;
        }
        if (_shutdown || _paused) yield break;
        if (_loginIpRequest.result == UnityWebRequest.Result.Success &&
            _loginIpRequest.responseCode >= 200 && _loginIpRequest.responseCode < 300)
        {
          var text = (_loginIpRequest.downloadHandler.text ?? "").Trim();
          IPAddress address;
          if (text.Length > 0 && text.Length <= 45 && text.IndexOf('%') < 0 &&
              IPAddress.TryParse(text, out address) &&
              (text.IndexOf(':') >= 0 || text == address.ToString()))
            ip = address.ToString();
        }
      }

      if (ip == "unknown") LogWarn("login_ip(unavailable) login will use unknown");
      CompleteStartupLogin(ip);
    }
    finally
    {
      if (_loginIpRequest != null) _loginIpRequest.Dispose();
      _loginIpRequest = null;
      _loginIpCoroutine = null;
    }
  }

  private void CompleteStartupLogin(string ip)
  {
    if (!_startupLoginPending || !IsReady || !IsReportingEnabled || _shutdown) return;
    _startupLoginPending = false;

    object normalized;
    int payloadBytes;
    var payload = new Dictionary<string, object> { { "ip", ip } };
    if (BizzaPayloadBudget.TryNormalize(payload, _options.maxRecordBytes - 2048, out normalized, out payloadBytes))
    {
      var record = BizzaRecord.NewEvent("login", (Dictionary<string, object>)normalized);
      record.ts = _startupLoginTimestamp;
      record.estimatedBytes = payloadBytes + 256 + record.eventName.Length * 6;
      if (EnqueueEventRealtimeRecord(record)) IncrementDailyRecordCount(UtcDateToday());
    }

    // Report every successful login lookup, including an unchanged IP. A failed
    // lookup must not replace a previously valid user property with unknown.
    if (ip != "unknown") ApplyUserProperty("update_ip", ip, false, "auto_login", true);
    SaveState();
    if (_config.verboseLog) LogVerbose("track(auto_event) event=login payload=" + SerializeForLog(payload));
    FlushNow("login_ip");
    FlushUserProfileNow("login_ip");
  }

  private void CancelLoginIpLookup()
  {
    if (_loginIpRequest != null) _loginIpRequest.Abort();
    _host.StopAnalyticsCoroutine(_loginIpCoroutine);
    // Unity may stop an iterator without executing its finally block.
    if (_loginIpRequest != null) _loginIpRequest.Dispose();
    _loginIpRequest = null;
    _loginIpCoroutine = null;
  }
}
