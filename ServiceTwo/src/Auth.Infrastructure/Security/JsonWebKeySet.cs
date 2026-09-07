using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Auth.Infrastructure.Security;

public sealed record JsonWebKey(string Kty, string Kid, string Use, string Alg, string N, string E);

public sealed record JsonWebKeySet(IReadOnlyList<JsonWebKey> Keys)
{
    public static JsonWebKeySet Create(RsaSigningKeyStore signingKeys)
    {
        RSAParameters parameters = signingKeys.Rsa.ExportParameters(includePrivateParameters: false);

        JsonWebKey key = new JsonWebKey(
            Kty: "RSA",
            Kid: signingKeys.KeyId,
            Use: "sig",
            Alg: "RS256",
            N: Base64UrlEncoder.Encode(parameters.Modulus),
            E: Base64UrlEncoder.Encode(parameters.Exponent));

        return new JsonWebKeySet(new[] { key });
    }
}
