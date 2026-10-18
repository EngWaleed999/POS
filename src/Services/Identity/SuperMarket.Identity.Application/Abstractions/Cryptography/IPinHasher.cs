// Cryptographic contract for hashing and verifying cashier POS PIN credentials.

namespace SuperMarket.Identity.Application.Abstractions.Cryptography;

public interface IPinHasher
{
    string HashPin(string rawPin);
    bool VerifyPin(string rawPin, string pinHash);
}
