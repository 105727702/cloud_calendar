using System;

namespace MyAvaloniaApp.Services
{
    public class PasswordManager
    {
        public string GenerateSalt()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var saltBytes = new byte[32];
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        public string HashPassword(string password, string salt)
        {
            using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
                password, 
                Convert.FromBase64String(salt), 
                100000, 
                System.Security.Cryptography.HashAlgorithmName.SHA256);
            return Convert.ToBase64String(pbkdf2.GetBytes(32));
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = HashPassword(password, salt);
            return computedHash == hash;
        }
    }
}
