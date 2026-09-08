using System.Security.Cryptography;
using System.Text;

namespace Cisharpai.Helpers;

/// <summary>
/// AES-GCM string encryption and decryption utility.
/// Uses PBKDF2 to derive a cryptographic key from a password, then encrypts/decrypts
/// using AES-GCM (authenticated encryption with associated data).
/// </summary>
/// <remarks>
/// Requires .NET 5.0 or later. The ciphertext format is:
/// <c>[salt (16 bytes)] [nonce (12 bytes)] [tag (16 bytes)] [ciphertext (variable)]</c>
/// encoded as a Base64 string.
/// </remarks>
public static class StringEncryptionHelper
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32; // AES-256
    private const int Pbkdf2Iterations = 100_000;

    /// <summary>
    /// Encrypts a plaintext string using AES-GCM with a password-derived key.
    /// </summary>
    /// <param name="plainText">The string to encrypt. Cannot be null or empty.</param>
    /// <param name="password">The password used to derive the encryption key. Cannot be null or empty.</param>
    /// <returns>A Base64-encoded string containing the salt, nonce, tag, and ciphertext.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="plainText"/> or <paramref name="password"/> is null or empty.</exception>
    public static string Encrypt(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text cannot be null or empty.", nameof(plainText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        byte[] salt = new byte[SaltSize];
        byte[] nonce = new byte[NonceSize];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(nonce);
        }

        byte[] key = DeriveKey(password, salt);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherText = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using (var aesGcm = new AesGcm(key))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
        }

        byte[] result = new byte[SaltSize + NonceSize + TagSize + cipherText.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherText, 0, result, SaltSize + NonceSize + TagSize, cipherText.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext string using AES-GCM with a password-derived key.
    /// </summary>
    /// <param name="cipherText">The Base64-encoded string to decrypt. Cannot be null or empty.</param>
    /// <param name="password">The password used to derive the decryption key. Cannot be null or empty.</param>
    /// <returns>The decrypted plaintext string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cipherText"/> or <paramref name="password"/> is null or empty.</exception>
    /// <exception cref="CryptographicException">Thrown when the password is incorrect or the data has been tampered with.</exception>
    public static string Decrypt(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("Cipher text cannot be null or empty.", nameof(cipherText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        byte[] buffer = Convert.FromBase64String(cipherText);

        if (buffer.Length < SaltSize + NonceSize + TagSize)
            throw new CryptographicException("Invalid cipher text.");

        byte[] salt = new byte[SaltSize];
        byte[] nonce = new byte[NonceSize];
        byte[] tag = new byte[TagSize];
        byte[] encrypted = new byte[buffer.Length - SaltSize - NonceSize - TagSize];

        Buffer.BlockCopy(buffer, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(buffer, SaltSize, nonce, 0, NonceSize);
        Buffer.BlockCopy(buffer, SaltSize + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(buffer, SaltSize + NonceSize + TagSize, encrypted, 0, encrypted.Length);

        byte[] key = DeriveKey(password, salt);
        byte[] decrypted = new byte[encrypted.Length];

        using (var aesGcm = new AesGcm(key))
        {
            try
            {
                aesGcm.Decrypt(nonce, encrypted, tag, decrypted);
            }
            catch (CryptographicException)
            {
                throw new CryptographicException("Decryption failed: invalid password or corrupted data.");
            }
        }

        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(KeySize);
    }
}
