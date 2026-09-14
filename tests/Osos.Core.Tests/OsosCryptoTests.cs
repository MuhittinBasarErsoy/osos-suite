using Osos.Core.Crypto;
using Xunit;

namespace Osos.Core.Tests;

public class OsosCryptoTests
{
    // Gerçek CryptoJS'ten (osos.uedas.com.tr sayfasında üretilen) altın test vektörü.
    private const string PassPhrase = "TESTPASS123";
    private const string SaltHex = "00112233445566778899aabbccddeeff";
    private const string IvHex = "ffeeddccbbaa99887766554433221100";
    private const string Plaintext =
        "{\"MethodName\":\"GetCustomerMarks\",\"Parameters\":675625,\"IsAsync\":false,\"Application\":\"PORTAL\"}";
    // Gerçek CryptoJS ile iterations=1000 (OSOS iterationCount=1e3) kullanılarak üretildi.
    private const string ExpectedKeyHex = "f76183a90d2e339ba4113bc68a304a59";
    private const string ExpectedCipherB64 =
        "3DbUR1jkwJX9cygp8GVRSwdxQ0Xqdz5KFUe/m396MCKcu5c2xdSDr4zADq4AJGR4dGpVlsz8uamBeEgj3ThhfO5u18lKUqGGxZvKvaN5fu19O3ZM8CFBNXZHClP1ucy6";

    [Fact]
    public void DeriveKey_MatchesCryptoJs()
    {
        byte[] key = OsosCrypto.DeriveKey(PassPhrase, OsosCrypto.FromHex(SaltHex));
        Assert.Equal(ExpectedKeyHex, OsosCrypto.ToHex(key));
    }

    [Fact]
    public void EncryptWith_MatchesCryptoJs()
    {
        string blob = OsosCrypto.EncryptWith(Plaintext, PassPhrase, OsosCrypto.FromHex(SaltHex), OsosCrypto.FromHex(IvHex));
        string expected = SaltHex + IvHex + ExpectedCipherB64;
        Assert.Equal(expected, blob);
    }

    [Fact]
    public void Decrypt_GoldenBlob_ReturnsPlaintext()
    {
        string blob = SaltHex + IvHex + ExpectedCipherB64;
        string? plain = OsosCrypto.Decrypt(blob, PassPhrase);
        Assert.Equal(Plaintext, plain);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrip()
    {
        string blob = OsosCrypto.Encrypt(Plaintext, OsosCrypto.ConstPassPhrase);
        string? plain = OsosCrypto.Decrypt(blob, OsosCrypto.ConstPassPhrase);
        Assert.Equal(Plaintext, plain);
    }
}
