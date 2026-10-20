using Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Application.Services
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltByteSize = 16;      // 128-bit salt
        private const int HashByteSize = 32;      // 256-bit subkey
        private const int Iterations = 100_000;   // OWASP recommendation for PBKDF2-HMAC-SHA256
        private const string FormatPrefix = "$pbkdf2$v=1$";

        public string HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = RandomNumberGenerator.GetBytes(SaltByteSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashByteSize);

            return $"{FormatPrefix}i={Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return false;
            }

            // Modern PBKDF2 format
            if (hashedPassword.StartsWith(FormatPrefix, StringComparison.Ordinal))
            {
                return VerifyPbkdf2(hashedPassword, providedPassword);
            }

            // Fallback: Legacy unsalted SHA-256
            return VerifyLegacySha256(hashedPassword, providedPassword);
        }

        public bool NeedsRehash(string hashedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword))
            {
                return true;
            }

            // If not modern PBKDF2 format, it needs an upgrade
            return !hashedPassword.StartsWith(FormatPrefix, StringComparison.Ordinal);
        }

        private static bool VerifyPbkdf2(string hashedPassword, string providedPassword)
        {
            try
            {
                // Format: $pbkdf2$v=1$i=100000$<base64salt>$<base64hash>
                var parts = hashedPassword.Split('$');
                if (parts.Length != 6) return false;

                var iterPart = parts[3];
                if (!iterPart.StartsWith("i=") || !int.TryParse(iterPart.Substring(2), out int iterations) || iterations <= 0)
                {
                    return false;
                }

                byte[] salt = Convert.FromBase64String(parts[4]);
                byte[] expectedHash = Convert.FromBase64String(parts[5]);

                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                    providedPassword,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyLegacySha256(string hashedPassword, string providedPassword)
        {
            try
            {
                using var sha256 = SHA256.Create();
                byte[] actualBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(providedPassword));
                string actualHashBase64 = Convert.ToBase64String(actualBytes);

                byte[] hashA = Encoding.UTF8.GetBytes(hashedPassword);
                byte[] hashB = Encoding.UTF8.GetBytes(actualHashBase64);

                return CryptographicOperations.FixedTimeEquals(hashA, hashB);
            }
            catch
            {
                return false;
            }
        }
    }
}
