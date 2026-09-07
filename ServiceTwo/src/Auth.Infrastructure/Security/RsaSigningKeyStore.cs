using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Auth.Infrastructure.Security;

public sealed class RsaSigningKeyStore : IDisposable
{
    private readonly RSA _rsa;

    public RsaSigningKeyStore(IOptions<JwtOptions> options)
    {
        _rsa = RSA.Create(2048);

        string path = options.Value.SigningKeyPath;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(path))
        {
            _rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            File.WriteAllText(path, _rsa.ExportRSAPrivateKeyPem());
        }

        KeyId = ComputeKeyId(_rsa);
    }

    public string KeyId { get; }

    public RSA Rsa
    {
        get { return _rsa; }
    }

    public void Dispose()
    {
        _rsa.Dispose();
    }

    private static string ComputeKeyId(RSA rsa)
    {
        byte[] publicKey = rsa.ExportRSAPublicKey();
        byte[] hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
