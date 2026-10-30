using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

internal static class BizzaPayloadBudget
{
  // Bound traversal before copying. Cycles fail at the depth limit without an unbounded walk.
  public static bool TryNormalize(object value, int maxBytes, out object result, out int bytes)
  {
    var remaining = maxBytes;
    var nodes = 2048;
    var ok = Copy(value, 0, ref remaining, ref nodes, out result);
    bytes = maxBytes - remaining;
    if (!ok) result = null;
    return ok;
  }

  private static bool Copy(object value, int depth, ref int remaining, ref int nodes, out object result)
  {
    result = null;
    if (depth > 8 || --nodes < 0 || remaining < 0) return false;
    if (value == null) { remaining -= 4; return remaining >= 0; }
    if (value is string)
    {
      result = value;
      remaining -= StringBytes((string)value, remaining);
      return remaining >= 0;
    }
    var dictionary = value as IDictionary;
    if (dictionary != null)
    {
      if (dictionary.Count > 256) return false;
      var copy = new Dictionary<string, object>(dictionary.Count);
      remaining -= 2;
      foreach (DictionaryEntry entry in dictionary)
      {
        var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(key) || key.Length > 256) return false;
        remaining -= StringBytes(key, remaining) + 2;
        object child;
        if (!Copy(entry.Value, depth + 1, ref remaining, ref nodes, out child)) return false;
        copy[key] = child;
      }
      result = copy;
      return remaining >= 0;
    }
    var list = value as IList;
    if (list != null)
    {
      if (list.Count > 256) return false;
      var copy = new List<object>(list.Count);
      remaining -= 2;
      foreach (var item in list)
      {
        remaining--;
        object child;
        if (!Copy(item, depth + 1, ref remaining, ref nodes, out child)) return false;
        copy.Add(child);
      }
      result = copy;
      return remaining >= 0;
    }
    if (value is double && (double.IsNaN((double)value) || double.IsInfinity((double)value))) return false;
    if (value is float && (float.IsNaN((float)value) || float.IsInfinity((float)value))) return false;
    if (value is bool || value is int || value is long || value is double || value is float || value is decimal)
    {
      remaining -= value is bool ? 5 : 32;
      result = value;
      return remaining >= 0;
    }
    if (value is short || value is byte)
    {
      remaining -= 32;
      result = Convert.ToInt32(value);
      return remaining >= 0;
    }
    result = BizzaValueUtil.Normalize(value);
    remaining -= StringBytes(Convert.ToString(result, CultureInfo.InvariantCulture), remaining);
    return remaining >= 0;
  }

  private static int StringBytes(string value, int budget)
  {
    if (value == null) return 4;
    if (value.Length > budget) return Math.Max(0, budget) + 1;
    var size = 2;
    foreach (var c in value)
    {
      // BizzaJson writes non-ASCII UTF-16 code units as six ASCII bytes (\uXXXX).
      size += c < 32 || c > 126 ? 6 : c == '"' || c == '\\' ? 2 : 1;
      if (size > budget) return Math.Max(0, budget) + 1;
    }
    return size;
  }
}
