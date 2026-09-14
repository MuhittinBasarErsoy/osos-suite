using System.Security.Cryptography;
using System.Text;

namespace Osos.Core.Crypto;

/// <summary>
/// OSOS portalının istemci tarafı AES şifrelemesinin birebir C# karşılığı.
/// CryptoJS ile uyumludur:
///   blob = saltHex(32) + ivHex(32) + Base64(ciphertext)
///   key  = PBKDF2(passPhrase, saltBytes, iterations=1, HMAC-SHA1, 16 byte)
///   AES-128-CBC / PKCS7, verilen IV ile.
/// passPhrase: login/nologin/forgotPassword için sabit anahtar; diğerlerinde login yanıtındaki SessionKey.
/// </summary>
public static class OsosCrypto
{
    /// <summary>login / nologin / forgotPassword uçlarında kullanılan sabit passPhrase.</summary>
    public const string ConstPassPhrase = "nQ16mjwHIrpH9IVobutgTTms8TuibkFagoWMNWguRckcqxQ";

    private const int KeySizeBytes = 16;   // 128 bit (keySize=128 / 32 = 4 word)
    private const int Iterations = 1000;   // OSOS: iterationCount = 1e3
    private const int SaltSizeBytes = 16;
    private const int IvSizeBytes = 16;

    /// <summary>Düz metni şifreler ve OSOS formatında blob döndürür (saltHex + ivHex + base64ct).</summary>
    public static string Encrypt(string plaintext, string passPhrase)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        return EncryptWith(plaintext, passPhrase, salt, iv);
    }

    /// <summary>Deterministik şifreleme (test/doğrulama için sabit salt+iv ile).</summary>
    public static string EncryptWith(string plaintext, string passPhrase, byte[] salt, byte[] iv)
    {
        byte[] key = DeriveKey(passPhrase, salt);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipher = aes.EncryptCbc(plainBytes, iv, PaddingMode.PKCS7);

        return ToHex(salt) + ToHex(iv) + Convert.ToBase64String(cipher);
    }

    /// <summary>OSOS blob'unu çözer. Başarısızsa null döner.</summary>
    public static string? Decrypt(string blob, string passPhrase)
    {
        if (string.IsNullOrEmpty(blob) || blob.Length < 64) return null;
        string saltHex = blob.Substring(0, 32);
        string ivHex = blob.Substring(32, 32);
        string ctB64 = blob.Substring(64);

        byte[] salt = FromHex(saltHex);
        byte[] iv = FromHex(ivHex);
        byte[] cipher = Convert.FromBase64String(ctB64);
        byte[] key = DeriveKey(passPhrase, salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        byte[] plain = aes.DecryptCbc(cipher, iv, PaddingMode.PKCS7);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>PBKDF2 (HMAC-SHA1, 1 tur) — CryptoJS.PBKDF2 varsayılanı.</summary>
    public static byte[] DeriveKey(string passPhrase, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passPhrase), salt, Iterations, HashAlgorithmName.SHA1, KeySizeBytes);
    }

    public static string ToHex(byte[] bytes) => Convert.ToHexStringLower(bytes);

    public static byte[] FromHex(string hex) => Convert.FromHexString(hex);
}
