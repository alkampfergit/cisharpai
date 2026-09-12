using System.Security.Cryptography;
using Cisharpai.Helpers;

namespace Cisharpai.Tests.Core;

public sealed class StringEncryptionHelperTests
{
    private const string TestPassword = "MySecretPassword123!";
    private const string TestPlaintext = "Hello, this is sensitive data!";

    [Test]
    public void Encrypt_ReturnsBase64String()
    {
        string result = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Not.Empty);
        Assert.DoesNotThrow(() => Convert.FromBase64String(result));
    }

    [Test]
    public void Encrypt_DifferentInputsProduceDifferentOutputs()
    {
        string encrypted1 = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);
        string encrypted2 = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);

        Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
    }

    [Test]
    public void Encrypt_WithDifferentPasswordsProducesDifferentOutputs()
    {
        string encrypted1 = StringEncryptionHelper.Encrypt(TestPlaintext, "Password1");
        string encrypted2 = StringEncryptionHelper.Encrypt(TestPlaintext, "Password2");

        Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
    }

    [Test]
    public void Decrypt_ReturnsOriginalPlaintext()
    {
        string encrypted = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);
        string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);

        Assert.That(decrypted, Is.EqualTo(TestPlaintext));
    }

    [Test]
    public void RoundTrip_SingleCharacter()
    {
        string encrypted = StringEncryptionHelper.Encrypt("a", TestPassword);
        string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);

        Assert.That(decrypted, Is.EqualTo("a"));
    }

    [Test]
    public void Decrypt_LongString()
    {
        string longPlaintext = new string('x', 10_000);
        string encrypted = StringEncryptionHelper.Encrypt(longPlaintext, TestPassword);
        string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);

        Assert.That(decrypted, Is.EqualTo(longPlaintext));
    }

    [Test]
    public void Decrypt_UnicodeString()
    {
        string unicodePlaintext = "こんにちは世界 🌍 Привет мир 🎉";
        string encrypted = StringEncryptionHelper.Encrypt(unicodePlaintext, TestPassword);
        string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);

        Assert.That(decrypted, Is.EqualTo(unicodePlaintext));
    }

    [Test]
    public void Decrypt_JsonString()
    {
        string jsonPlaintext = """{"key":"value","nested":{"array":[1,2,3]}}""";
        string encrypted = StringEncryptionHelper.Encrypt(jsonPlaintext, TestPassword);
        string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);

        Assert.That(decrypted, Is.EqualTo(jsonPlaintext));
    }

    [Test]
    public void Decrypt_WrongPassword_ThrowsCryptographicException()
    {
        string encrypted = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);

        Assert.Throws<CryptographicException>(() => StringEncryptionHelper.Decrypt(encrypted, "WrongPassword"));
    }

    [Test]
    public void Decrypt_TamperedData_ThrowsCryptographicException()
    {
        string encrypted = StringEncryptionHelper.Encrypt(TestPlaintext, TestPassword);

        // Flip a character in the Base64 string
        char[] chars = encrypted.ToCharArray();
        chars[5] = chars[5] == 'A' ? 'B' : 'A';
        string tampered = new string(chars);

        Assert.Throws<CryptographicException>(() => StringEncryptionHelper.Decrypt(tampered, TestPassword));
    }

    [Test]
    public void Encrypt_NullPlainText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Encrypt(null!, TestPassword));
    }

    [Test]
    public void Encrypt_EmptyPlainText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Encrypt(string.Empty, TestPassword));
    }

    [Test]
    public void Encrypt_NullPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Encrypt(TestPlaintext, null!));
    }

    [Test]
    public void Encrypt_EmptyPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Encrypt(TestPlaintext, string.Empty));
    }

    [Test]
    public void Decrypt_NullCipherText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Decrypt(null!, TestPassword));
    }

    [Test]
    public void Decrypt_EmptyCipherText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Decrypt(string.Empty, TestPassword));
    }

    [Test]
    public void Decrypt_NullPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Decrypt("dGVzdA==", null!));
    }

    [Test]
    public void Decrypt_EmptyPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StringEncryptionHelper.Decrypt("dGVzdA==", string.Empty));
    }

    [Test]
    public void Decrypt_InvalidBase64_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => StringEncryptionHelper.Decrypt("not-valid-base64!!!", TestPassword));
    }

    [Test]
    public void Decrypt_TooShortCipherText_ThrowsCryptographicException()
    {
        // Valid Base64 but too short to contain salt + nonce + tag
        string shortCipherText = Convert.ToBase64String(new byte[5]);

        Assert.Throws<CryptographicException>(() => StringEncryptionHelper.Decrypt(shortCipherText, TestPassword));
    }

    [Test]
    public void Encrypt_Decrypt_RoundTripMultipleTimes()
    {
        string original = "Round trip test data";

        for (int i = 0; i < 10; i++)
        {
            string encrypted = StringEncryptionHelper.Encrypt(original, TestPassword);
            string decrypted = StringEncryptionHelper.Decrypt(encrypted, TestPassword);
            Assert.That(decrypted, Is.EqualTo(original));
        }
    }
}
