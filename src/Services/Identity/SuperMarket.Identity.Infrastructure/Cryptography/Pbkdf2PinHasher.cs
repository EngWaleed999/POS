// Cryptographic implementation of cashier POS PIN hashing using PBKDF2-SHA256 and constant-time verification.

using System.Security.Cryptography;
using SuperMarket.Identity.Application.Abstractions.Cryptography;

namespace SuperMarket.Identity.Infrastructure.Cryptography;

public sealed class Pbkdf2PinHasher : IPinHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

    public string HashPin(string rawPin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPin);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            rawPin,
            salt,
            Iterations,
            HashAlgorithm,
            KeySize);

        return $"{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    public bool VerifyPin(string rawPin, string pinHash)
    {
        if (string.IsNullOrWhiteSpace(rawPin) || string.IsNullOrWhiteSpace(pinHash))
            return false;

        var parts = pinHash.Split(':');
        if (parts.Length != 2)
            return false;

        try
        {
            byte[] salt = Convert.FromHexString(parts[0]);
            byte[] expectedHash = Convert.FromHexString(parts[1]);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                rawPin,
                salt,
                Iterations,
                HashAlgorithm,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
