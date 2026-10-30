using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

internal sealed class BizzaCrypto : IDisposable
{
  private const int RequiredKeyBytes = 32;
  private const int IvBytes = 12;
  private const int BlockBytes = 16;
  private const int TagBytes = 16;
  private const string EnvelopeVersion = "enc-v1";
  private const string EnvelopeAlgorithm = "A256GCM";
  private const ulong GaloisRHi = 0xE100000000000000UL;

  private readonly byte[] _keyBytes;
  private readonly string _kid;
  private readonly Aes _aes;
  private readonly ICryptoTransform _encryptor;
  private readonly object _encryptLock = new object();
  private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
  private readonly ulong _hashSubkeyHi;
  private readonly ulong _hashSubkeyLo;

  private BizzaCrypto(byte[] keyBytes, string kid)
  {
    _keyBytes = keyBytes;
    _kid = kid;

    _aes = new AesManaged
    {
      KeySize = RequiredKeyBytes * 8,
      BlockSize = BlockBytes * 8,
      Mode = CipherMode.ECB,
      Padding = PaddingMode.None,
      Key = _keyBytes,
    };
    _encryptor = _aes.CreateEncryptor();

    var hashSubkey = new byte[BlockBytes];
    EncryptBlock(new byte[BlockBytes], 0, hashSubkey, 0);
    _hashSubkeyHi = ReadUInt64BE(hashSubkey, 0);
    _hashSubkeyLo = ReadUInt64BE(hashSubkey, 8);
  }

  public static bool TryCreate(string keyBase64, string kid, out BizzaCrypto crypto, out string error)
  {
    crypto = null;
    error = "";

    if (string.IsNullOrWhiteSpace(keyBase64))
    {
      error = "encryption_key_base64_is_empty";
      return false;
    }

    byte[] rawKey;
    try
    {
      rawKey = Convert.FromBase64String(keyBase64.Trim());
    }
    catch (Exception ex)
    {
      error = "encryption_key_base64_invalid:" + ex.Message;
      return false;
    }

    if (rawKey.Length != RequiredKeyBytes)
    {
      error = "encryption_key_must_be_32_bytes";
      return false;
    }

    var normalizedKid = string.IsNullOrWhiteSpace(kid) ? "sdk-default-kid" : kid.Trim();
    var stableKey = new byte[rawKey.Length];
    Buffer.BlockCopy(rawKey, 0, stableKey, 0, rawKey.Length);

    crypto = new BizzaCrypto(stableKey, normalizedKid);
    return true;
  }

  public Dictionary<string, object> EncryptValue(object value)
  {
    return EncryptNormalizedValue(BizzaValueUtil.Normalize(value));
  }

  internal Dictionary<string, object> EncryptNormalizedValue(object value)
  {
    var plainJson = BizzaJson.Serialize(value);
    var plainBytes = Encoding.UTF8.GetBytes(plainJson);

    var iv = new byte[IvBytes];
    lock (_encryptLock)
    {
      _rng.GetBytes(iv);
    }

    var ciphertext = new byte[plainBytes.Length];
    var tag = new byte[TagBytes];
    EncryptAesGcm(iv, plainBytes, ciphertext, tag);

    var encPayload = new Dictionary<string, object>(6);
    encPayload["v"] = EnvelopeVersion;
    encPayload["alg"] = EnvelopeAlgorithm;
    encPayload["kid"] = _kid;
    encPayload["iv"] = Convert.ToBase64String(iv);
    encPayload["ct"] = Convert.ToBase64String(ciphertext);
    encPayload["tag"] = Convert.ToBase64String(tag);

    var wrappedPayload = new Dictionary<string, object>(1);
    wrappedPayload["__enc__"] = encPayload;
    return wrappedPayload;
  }

  public void Dispose()
  {
    lock (_encryptLock)
    {
      _rng.Dispose();
      _encryptor.Dispose();
      _aes.Dispose();
    }
  }

  public bool TryDecryptValue(object wrappedValue, out object value, out string error)
  {
    value = null;
    error = "";

    var wrappedPayload = BizzaValueUtil.AsStringObjectDictionary(wrappedValue);
    if (!wrappedPayload.ContainsKey("__enc__"))
    {
      error = "encrypted_payload_missing";
      return false;
    }

    var encPayload = BizzaValueUtil.AsStringObjectDictionary(wrappedPayload["__enc__"]);
    var version = BizzaValueUtil.AsString(encPayload.ContainsKey("v") ? encPayload["v"] : null, "");
    if (version != EnvelopeVersion)
    {
      error = "encrypted_payload_version_invalid";
      return false;
    }

    var algorithm = BizzaValueUtil.AsString(encPayload.ContainsKey("alg") ? encPayload["alg"] : null, "");
    if (algorithm != EnvelopeAlgorithm)
    {
      error = "encrypted_payload_algorithm_invalid";
      return false;
    }

    byte[] iv;
    byte[] ciphertext;
    byte[] tag;
    if (!TryReadBase64(encPayload, "iv", IvBytes, out iv, out error) ||
        !TryReadBase64(encPayload, "ct", -1, out ciphertext, out error) ||
        !TryReadBase64(encPayload, "tag", TagBytes, out tag, out error))
    {
      return false;
    }

    var j0 = CreateInitialCounterBlock(iv);
    var expectedTag = new byte[TagBytes];
    ComputeTag(j0, ciphertext, expectedTag);
    if (!FixedTimeEquals(tag, expectedTag))
    {
      error = "encrypted_payload_tag_invalid";
      return false;
    }

    var plainBytes = new byte[ciphertext.Length];
    DecryptAesGcm(iv, ciphertext, plainBytes);
    var plainJson = Encoding.UTF8.GetString(plainBytes);

    try
    {
      value = BizzaJson.Deserialize(plainJson);
      return true;
    }
    catch (Exception ex)
    {
      error = "encrypted_payload_json_invalid:" + ex.Message;
      return false;
    }
  }

  private void EncryptAesGcm(byte[] iv, byte[] plaintext, byte[] ciphertext, byte[] tag)
  {
    var counterBlock = new byte[BlockBytes];
    var streamBlock = new byte[BlockBytes];
    var j0 = CreateInitialCounterBlock(iv);
    Buffer.BlockCopy(j0, 0, counterBlock, 0, BlockBytes);

    var offset = 0;
    while (offset < plaintext.Length)
    {
      IncrementCounter(counterBlock);
      EncryptBlock(counterBlock, 0, streamBlock, 0);

      var blockLength = Math.Min(BlockBytes, plaintext.Length - offset);
      for (var i = 0; i < blockLength; i++)
      {
        ciphertext[offset + i] = (byte)(plaintext[offset + i] ^ streamBlock[i]);
      }

      offset += blockLength;
    }

    ComputeTag(j0, ciphertext, tag);
  }

  private void DecryptAesGcm(byte[] iv, byte[] ciphertext, byte[] plaintext)
  {
    var counterBlock = new byte[BlockBytes];
    var streamBlock = new byte[BlockBytes];
    var j0 = CreateInitialCounterBlock(iv);
    Buffer.BlockCopy(j0, 0, counterBlock, 0, BlockBytes);

    var offset = 0;
    while (offset < ciphertext.Length)
    {
      IncrementCounter(counterBlock);
      EncryptBlock(counterBlock, 0, streamBlock, 0);

      var blockLength = Math.Min(BlockBytes, ciphertext.Length - offset);
      for (var i = 0; i < blockLength; i++)
      {
        plaintext[offset + i] = (byte)(ciphertext[offset + i] ^ streamBlock[i]);
      }

      offset += blockLength;
    }
  }

  private void ComputeTag(byte[] j0, byte[] ciphertext, byte[] tag)
  {
    var auth = new byte[BlockBytes];
    var offset = 0;

    while (offset < ciphertext.Length)
    {
      var blockLength = Math.Min(BlockBytes, ciphertext.Length - offset);
      XorPartial(auth, ciphertext, offset, blockLength);
      MultiplyByHashSubkey(auth);
      offset += blockLength;
    }

    var lengthBlock = new byte[BlockBytes];
    WriteUInt64BE(lengthBlock, 8, checked((ulong)ciphertext.LongLength * 8UL));
    XorBlock(auth, lengthBlock);
    MultiplyByHashSubkey(auth);

    var encryptedJ0 = new byte[BlockBytes];
    EncryptBlock(j0, 0, encryptedJ0, 0);
    for (var i = 0; i < BlockBytes; i++)
    {
      tag[i] = (byte)(auth[i] ^ encryptedJ0[i]);
    }
  }

  private void MultiplyByHashSubkey(byte[] block)
  {
    var zHi = 0UL;
    var zLo = 0UL;
    var vHi = _hashSubkeyHi;
    var vLo = _hashSubkeyLo;
    var xHi = ReadUInt64BE(block, 0);
    var xLo = ReadUInt64BE(block, 8);

    MultiplyWord(xHi, ref zHi, ref zLo, ref vHi, ref vLo);
    MultiplyWord(xLo, ref zHi, ref zLo, ref vHi, ref vLo);

    WriteUInt64BE(block, 0, zHi);
    WriteUInt64BE(block, 8, zLo);
  }

  private static void MultiplyWord(ulong word, ref ulong zHi, ref ulong zLo, ref ulong vHi, ref ulong vLo)
  {
    for (var bit = 63; bit >= 0; bit--)
    {
      if (((word >> bit) & 1UL) != 0)
      {
        zHi ^= vHi;
        zLo ^= vLo;
      }

      var lsb = (vLo & 1UL) != 0;
      vLo = (vLo >> 1) | (vHi << 63);
      vHi >>= 1;
      if (lsb)
      {
        vHi ^= GaloisRHi;
      }
    }
  }

  private void EncryptBlock(byte[] input, int inputOffset, byte[] output, int outputOffset)
  {
    lock (_encryptLock)
    {
      _encryptor.TransformBlock(input, inputOffset, BlockBytes, output, outputOffset);
    }
  }

  private static void IncrementCounter(byte[] counterBlock)
  {
    for (var i = BlockBytes - 1; i >= BlockBytes - 4; i--)
    {
      counterBlock[i] += 1;
      if (counterBlock[i] != 0)
      {
        break;
      }
    }
  }

  private static void XorPartial(byte[] accumulator, byte[] source, int sourceOffset, int sourceLength)
  {
    for (var i = 0; i < sourceLength; i++)
    {
      accumulator[i] ^= source[sourceOffset + i];
    }
  }

  private static void XorBlock(byte[] left, byte[] right)
  {
    for (var i = 0; i < BlockBytes; i++)
    {
      left[i] ^= right[i];
    }
  }

  private static ulong ReadUInt64BE(byte[] buffer, int offset)
  {
    return
      ((ulong)buffer[offset] << 56) |
      ((ulong)buffer[offset + 1] << 48) |
      ((ulong)buffer[offset + 2] << 40) |
      ((ulong)buffer[offset + 3] << 32) |
      ((ulong)buffer[offset + 4] << 24) |
      ((ulong)buffer[offset + 5] << 16) |
      ((ulong)buffer[offset + 6] << 8) |
      buffer[offset + 7];
  }

  private static void WriteUInt64BE(byte[] buffer, int offset, ulong value)
  {
    buffer[offset] = (byte)(value >> 56);
    buffer[offset + 1] = (byte)(value >> 48);
    buffer[offset + 2] = (byte)(value >> 40);
    buffer[offset + 3] = (byte)(value >> 32);
    buffer[offset + 4] = (byte)(value >> 24);
    buffer[offset + 5] = (byte)(value >> 16);
    buffer[offset + 6] = (byte)(value >> 8);
    buffer[offset + 7] = (byte)value;
  }

  private static bool TryReadBase64(Dictionary<string, object> map, string key, int expectedBytes, out byte[] value, out string error)
  {
    value = null;
    error = "";

    if (map == null || !map.ContainsKey(key))
    {
      error = "encrypted_payload_" + key + "_missing";
      return false;
    }

    var text = BizzaValueUtil.AsString(map[key], "");
    try
    {
      value = Convert.FromBase64String(text);
    }
    catch (Exception ex)
    {
      error = "encrypted_payload_" + key + "_base64_invalid:" + ex.Message;
      return false;
    }

    if (expectedBytes >= 0 && value.Length != expectedBytes)
    {
      error = "encrypted_payload_" + key + "_length_invalid";
      return false;
    }

    return true;
  }

  private static byte[] CreateInitialCounterBlock(byte[] iv)
  {
    var j0 = new byte[BlockBytes];
    Buffer.BlockCopy(iv, 0, j0, 0, IvBytes);
    j0[BlockBytes - 1] = 1;
    return j0;
  }

  private static bool FixedTimeEquals(byte[] left, byte[] right)
  {
    if (left == null || right == null)
    {
      return false;
    }

    var diff = left.Length ^ right.Length;
    var count = Math.Min(left.Length, right.Length);
    for (var i = 0; i < count; i++)
    {
      diff |= left[i] ^ right[i];
    }

    return diff == 0;
  }
}
