using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

internal static class BizzaJson
{
  public static object Deserialize(string json)
  {
    if (string.IsNullOrEmpty(json))
    {
      return null;
    }
    return Parser.Parse(json);
  }

  public static string Serialize(object obj)
  {
    return Serializer.Serialize(obj);
  }

  private sealed class Parser : IDisposable
  {
    private enum Token
    {
      None,
      CurlyOpen,
      CurlyClose,
      SquaredOpen,
      SquaredClose,
      Colon,
      Comma,
      String,
      Number,
      True,
      False,
      Null,
    }

    private readonly StringReader _reader;

    private Parser(string json)
    {
      _reader = new StringReader(json);
    }

    public static object Parse(string json)
    {
      using (var parser = new Parser(json))
      {
        return parser.ParseValue();
      }
    }

    public void Dispose()
    {
      _reader.Dispose();
    }

    private Dictionary<string, object> ParseObject()
    {
      var table = new Dictionary<string, object>();
      _reader.Read();

      while (true)
      {
        var token = NextToken;
        if (token == Token.None)
        {
          return null;
        }
        if (token == Token.Comma)
        {
          continue;
        }
        if (token == Token.CurlyClose)
        {
          return table;
        }

        var name = ParseString();
        if (name == null)
        {
          return null;
        }

        if (NextToken != Token.Colon)
        {
          return null;
        }
        _reader.Read();

        table[name] = ParseValue();
      }
    }

    private List<object> ParseArray()
    {
      var array = new List<object>();
      _reader.Read();

      var parsing = true;
      while (parsing)
      {
        var token = NextToken;
        if (token == Token.None)
        {
          return null;
        }
        if (token == Token.Comma)
        {
          continue;
        }
        if (token == Token.SquaredClose)
        {
          break;
        }

        array.Add(ParseByToken(token));
      }
      return array;
    }

    private object ParseValue()
    {
      return ParseByToken(NextToken);
    }

    private object ParseByToken(Token token)
    {
      switch (token)
      {
        case Token.String:
          return ParseString();
        case Token.Number:
          return ParseNumber();
        case Token.CurlyOpen:
          return ParseObject();
        case Token.SquaredOpen:
          return ParseArray();
        case Token.True:
          return true;
        case Token.False:
          return false;
        case Token.Null:
          return null;
        default:
          return null;
      }
    }

    private string ParseString()
    {
      var builder = new StringBuilder();
      char c;

      _reader.Read();

      var parsing = true;
      while (parsing)
      {
        if (_reader.Peek() == -1)
        {
          break;
        }

        c = NextChar;
        if (c == '"')
        {
          parsing = false;
          break;
        }
        if (c == '\\')
        {
          if (_reader.Peek() == -1)
          {
            parsing = false;
            break;
          }

          c = NextChar;
          if (c == '"') builder.Append('"');
          else if (c == '\\') builder.Append('\\');
          else if (c == '/') builder.Append('/');
          else if (c == 'b') builder.Append('\b');
          else if (c == 'f') builder.Append('\f');
          else if (c == 'n') builder.Append('\n');
          else if (c == 'r') builder.Append('\r');
          else if (c == 't') builder.Append('\t');
          else if (c == 'u')
          {
            var hex = new char[4];
            for (var i = 0; i < 4; i++)
            {
              hex[i] = NextChar;
            }
            builder.Append((char)Convert.ToInt32(new string(hex), 16));
          }
        }
        else
        {
          builder.Append(c);
        }
      }

      return builder.ToString();
    }

    private object ParseNumber()
    {
      var number = NextWord;
      if (number.IndexOf('.') == -1)
      {
        long parsedInt;
        if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt))
        {
          return parsedInt;
        }
      }

      double parsedDouble;
      if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDouble))
      {
        return parsedDouble;
      }

      return 0d;
    }

    private void EatWhitespace()
    {
      while (char.IsWhiteSpace(PeekChar))
      {
        _reader.Read();
        if (_reader.Peek() == -1)
        {
          break;
        }
      }
    }

    private char PeekChar
    {
      get { return Convert.ToChar(_reader.Peek()); }
    }

    private char NextChar
    {
      get { return Convert.ToChar(_reader.Read()); }
    }

    private string NextWord
    {
      get
      {
        var word = new StringBuilder();
        while (!IsWordBreak(PeekChar))
        {
          word.Append(NextChar);
          if (_reader.Peek() == -1)
          {
            break;
          }
        }
        return word.ToString();
      }
    }

    private Token NextToken
    {
      get
      {
        EatWhitespace();
        if (_reader.Peek() == -1)
        {
          return Token.None;
        }

        var c = PeekChar;
        if (c == '{') return Token.CurlyOpen;
        if (c == '}')
        {
          _reader.Read();
          return Token.CurlyClose;
        }
        if (c == '[') return Token.SquaredOpen;
        if (c == ']')
        {
          _reader.Read();
          return Token.SquaredClose;
        }
        if (c == ',')
        {
          _reader.Read();
          return Token.Comma;
        }
        if (c == '"') return Token.String;
        if (c == ':') return Token.Colon;
        if (char.IsDigit(c) || c == '-') return Token.Number;

        var word = NextWord;
        if (word == "false") return Token.False;
        if (word == "true") return Token.True;
        if (word == "null") return Token.Null;

        return Token.None;
      }
    }

    private static bool IsWordBreak(char c)
    {
      return char.IsWhiteSpace(c) || c == ',' || c == ':' || c == ']' || c == '}' || c == '[' || c == '{' || c == '"';
    }
  }

  private sealed class Serializer
  {
    private const int InitialBuilderCapacity = 256;
    private const int MaxCachedBuilderCapacity = 8192;
    [ThreadStatic] private static StringBuilder _cachedBuilder;

    private readonly StringBuilder _builder;

    private Serializer(StringBuilder builder)
    {
      _builder = builder;
    }

    public static string Serialize(object obj)
    {
      var builder = AcquireBuilder();
      try
      {
        var instance = new Serializer(builder);
        instance.SerializeValue(obj);
        return builder.ToString();
      }
      finally
      {
        ReleaseBuilder(builder);
      }
    }

    private static StringBuilder AcquireBuilder()
    {
      var builder = _cachedBuilder;
      if (builder != null)
      {
        _cachedBuilder = null;
        builder.Length = 0;
        return builder;
      }

      return new StringBuilder(InitialBuilderCapacity);
    }

    private static void ReleaseBuilder(StringBuilder builder)
    {
      if (builder == null)
      {
        return;
      }

      if (builder.Capacity > MaxCachedBuilderCapacity)
      {
        return;
      }

      builder.Length = 0;
      _cachedBuilder = builder;
    }

    private void SerializeValue(object value)
    {
      if (value == null)
      {
        _builder.Append("null");
        return;
      }

      var str = value as string;
      if (str != null)
      {
        SerializeString(str);
        return;
      }

      if (value is bool)
      {
        _builder.Append((bool)value ? "true" : "false");
        return;
      }

      var dict = value as IDictionary;
      if (dict != null)
      {
        SerializeObject(dict);
        return;
      }

      var array = value as IList;
      if (array != null)
      {
        SerializeArray(array);
        return;
      }

      if (IsNumeric(value))
      {
        SerializeNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        return;
      }

      SerializeString(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private void SerializeObject(IDictionary obj)
    {
      var first = true;
      _builder.Append('{');

      foreach (DictionaryEntry entry in obj)
      {
        if (!first)
        {
          _builder.Append(',');
        }
        SerializeString(Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
        _builder.Append(':');
        SerializeValue(entry.Value);
        first = false;
      }

      _builder.Append('}');
    }

    private void SerializeArray(IList array)
    {
      _builder.Append('[');
      var first = true;
      for (var i = 0; i < array.Count; i++)
      {
        if (!first)
        {
          _builder.Append(',');
        }
        SerializeValue(array[i]);
        first = false;
      }
      _builder.Append(']');
    }

    private void SerializeString(string str)
    {
      _builder.Append('"');

      for (var i = 0; i < str.Length; i++)
      {
        var c = str[i];
        if (c == '"') _builder.Append("\\\"");
        else if (c == '\\') _builder.Append("\\\\");
        else if (c == '\b') _builder.Append("\\b");
        else if (c == '\f') _builder.Append("\\f");
        else if (c == '\n') _builder.Append("\\n");
        else if (c == '\r') _builder.Append("\\r");
        else if (c == '\t') _builder.Append("\\t");
        else
        {
          var codepoint = Convert.ToInt32(c);
          if (codepoint >= 32 && codepoint <= 126)
          {
            _builder.Append(c);
          }
          else
          {
            _builder.Append("\\u");
            _builder.Append(codepoint.ToString("x4"));
          }
        }
      }

      _builder.Append('"');
    }

    private void SerializeNumber(double number)
    {
      _builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
    }

    private static bool IsNumeric(object value)
    {
      return value is sbyte || value is byte || value is short || value is ushort ||
             value is int || value is uint || value is long || value is ulong ||
             value is float || value is double || value is decimal;
    }
  }
}
