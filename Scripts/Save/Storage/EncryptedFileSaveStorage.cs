using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Zaomeng.Save.Storage;

/// <summary>AES-256-CBC 加密，HMAC-SHA256 对文件头、IV 和密文整体验签。</summary>
public sealed class EncryptedFileSaveStorage : FileSaveStorage
{
	private static readonly byte[] Magic = "ZMS1"u8.ToArray();
	private const int IvLength = 16;
	private const int TagLength = 32;
	private readonly byte[] _encryptionKey;
	private readonly byte[] _authenticationKey;

	public EncryptedFileSaveStorage(string directory, byte[] masterKey) : base(directory, "dat")
	{
		ArgumentNullException.ThrowIfNull(masterKey);
		if (masterKey.Length != 32)
			throw new ArgumentException("主密钥必须是 32 字节。", nameof(masterKey));
		// 用不同用途的子密钥，避免加密和验签复用同一密钥。
		_encryptionKey = HMACSHA256.HashData(masterKey, Encoding.ASCII.GetBytes("Zaomeng save encryption v1"));
		_authenticationKey = HMACSHA256.HashData(masterKey, Encoding.ASCII.GetBytes("Zaomeng save authentication v1"));
	}

	protected override byte[] Encode(byte[] data)
	{
		using Aes aes = Aes.Create();
		aes.Key = _encryptionKey;
		aes.GenerateIV();
		using ICryptoTransform encryptor = aes.CreateEncryptor();
		byte[] ciphertext = encryptor.TransformFinalBlock(data, 0, data.Length);
		byte[] output = new byte[Magic.Length + IvLength + ciphertext.Length + TagLength];
		Magic.CopyTo(output, 0);
		aes.IV.CopyTo(output, Magic.Length);
		ciphertext.CopyTo(output, Magic.Length + IvLength);
		byte[] tag = HMACSHA256.HashData(_authenticationKey, output.AsSpan(0, output.Length - TagLength));
		tag.CopyTo(output, output.Length - TagLength);
		return output;
	}

	protected override byte[] Decode(byte[] data)
	{
		int headerLength = Magic.Length + IvLength;
		int ciphertextLength = data.Length - headerLength - TagLength;
		if (ciphertextLength < IvLength || ciphertextLength % IvLength != 0 ||
			!data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
			throw new InvalidDataException("存档格式无效或加密版本不受支持。");
		ReadOnlySpan<byte> content = data.AsSpan(0, data.Length - TagLength);
		byte[] expectedTag = HMACSHA256.HashData(_authenticationKey, content);
		if (!CryptographicOperations.FixedTimeEquals(expectedTag, data.AsSpan(data.Length - TagLength)))
			throw new CryptographicException("存档验签失败，文件可能已损坏或使用了错误的密钥。");
		using Aes aes = Aes.Create();
		aes.Key = _encryptionKey;
		aes.IV = data.AsSpan(Magic.Length, IvLength).ToArray();
		using ICryptoTransform decryptor = aes.CreateDecryptor();
		return decryptor.TransformFinalBlock(data, headerLength, ciphertextLength);
	}
}
