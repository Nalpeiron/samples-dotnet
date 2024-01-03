using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Webhooks.Console;

public sealed class PublicRsaKey : IDisposable
{
    private const int RsaKeySize = 2048;
    private readonly RSA _rsa;
    
    private PublicRsaKey(RSA rsa)
    {
        _rsa = rsa;
    }
    
    public static bool TryCreate(string keyModulus, out PublicRsaKey? publicRsaKey)
    {
        try
        {
            var rsaKey = new RSACryptoServiceProvider(RsaKeySize);
            rsaKey.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlEncoder.DecodeBytes(keyModulus),
                Exponent = Base64UrlEncoder.DecodeBytes("AQAB")
            });
            publicRsaKey = new PublicRsaKey(rsaKey);
            return true;
        }
        catch (Exception)
        {
            publicRsaKey = null;
            return false;
        }
    }
    
    public bool VerifySignature(string plainText, string signature)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return _rsa.VerifyHash(hash, Convert.FromBase64String(signature), HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }
}