using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal sealed class BizzaStateStore
{
  private static readonly Dictionary<string, BizzaStateStore> Stores = new Dictionary<string, BizzaStateStore>();
  private readonly string _path;
  private readonly bool _verboseLog;
  private readonly object _gate = new object();
  private BizzaState _latestSnapshot;
  private BizzaState _pendingSnapshot;
  private long _requestedVersion;
  private long _completedVersion;
  private long _savedVersion;
  private bool _writing;
  private string _writeError = "";

  // Share the writer across agent recreation so an older session cannot overwrite a newer one.
  public static BizzaStateStore Open(string persistentDataPath, bool verboseLog)
  {
    var path = Path.GetFullPath(persistentDataPath);
    lock (Stores)
    {
      BizzaStateStore store;
      if (!Stores.TryGetValue(path, out store))
      {
        store = new BizzaStateStore(path, verboseLog);
        Stores.Add(path, store);
      }
      return store;
    }
  }

  public BizzaStateStore(string persistentDataPath, bool verboseLog)
  {
    _path = Path.Combine(persistentDataPath, "bizza_analytics_state.json");
    _verboseLog = verboseLog;
  }

  public BizzaState Load()
  {
    lock (_gate)
    {
      if (_latestSnapshot != null) return _latestSnapshot.Snapshot();
    }
    foreach (var candidate in new[] { _path, _path + ".bak", _path + ".tmp" })
    {
      try
      {
        if (!File.Exists(candidate)) continue;
        var parsed = BizzaJson.Deserialize(File.ReadAllText(candidate, Encoding.UTF8)) as Dictionary<string, object>;
        if (parsed == null || !parsed.ContainsKey("uid")) continue;
        return BizzaState.FromStateObject(parsed);
      }
      catch (Exception ex)
      {
        if (_verboseLog) Debug.LogWarning("[BizzaAnalytics] Failed to load state candidate. " + ex.Message);
      }
    }
    return BizzaState.CreateDefault();
  }

  public long EnqueueSave(BizzaState snapshot)
  {
    lock (_gate)
    {
      _latestSnapshot = _pendingSnapshot = snapshot;
      var version = ++_requestedVersion;
      if (!_writing)
      {
        _writing = true;
        Task.Run((Action)WritePending);
      }
      return version;
    }
  }

  public bool GetSaveResult(long version, out string error)
  {
    lock (_gate)
    {
      error = _savedVersion >= version ? "" : _writeError;
      return _completedVersion >= version;
    }
  }

  public bool WaitForSave(long version, int milliseconds)
  {
    var timer = System.Diagnostics.Stopwatch.StartNew();
    lock (_gate)
    {
      while (_completedVersion < version)
      {
        var remaining = milliseconds - (int)timer.ElapsedMilliseconds;
        if (remaining <= 0) return false;
        Monitor.Wait(_gate, remaining);
      }
      return _savedVersion >= version;
    }
  }

  // Synchronous helper for offline tools; runtime code uses EnqueueSave.
  public void Save(BizzaState state)
  {
    var version = EnqueueSave(state.Snapshot());
    if (!WaitForSave(version, 30000)) throw new IOException("Analytics state save failed or timed out.");
  }

  private void WritePending()
  {
    while (true)
    {
      BizzaState snapshot;
      long version;
      lock (_gate)
      {
        snapshot = _pendingSnapshot;
        if (snapshot == null)
        {
          _writing = false;
          return;
        }
        version = _requestedVersion;
        _pendingSnapshot = null;
      }
      var error = "";
      try { WriteSnapshot(snapshot); }
      catch (Exception ex) { error = ex.Message; }
      lock (_gate)
      {
        _completedVersion = version;
        if (error.Length == 0) _savedVersion = version;
        _writeError = error;
        Monitor.PulseAll(_gate);
      }
    }
  }

  private void WriteSnapshot(BizzaState snapshot)
  {
    var json = BizzaJson.Serialize(snapshot.ToStateObject());
    Directory.CreateDirectory(Path.GetDirectoryName(_path));
    var temporary = _path + ".tmp";
    var backup = _path + ".bak";
    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
    {
      using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
      {
        writer.Write(json);
        writer.Flush();
      }
      stream.Flush(true);
    }
    if (!File.Exists(_path))
    {
      File.Move(temporary, _path);
      return;
    }
    try { File.Replace(temporary, _path, backup); }
    catch (PlatformNotSupportedException) { ReplaceByRename(temporary, backup); }
    catch (NotSupportedException) { ReplaceByRename(temporary, backup); }
  }

  private void ReplaceByRename(string temporary, string backup)
  {
    if (File.Exists(backup)) File.Delete(backup);
    File.Move(_path, backup);
    File.Move(temporary, _path);
  }
}
