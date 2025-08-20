using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class JwtService
    {
        private static JwtService? _instance;
        public static JwtService Instance => _instance ??= new JwtService();

        private readonly string _secretKey;
        private readonly string _tokenFilePath;
        
        private JwtService()
        {
            // Tạo secret key unique cho mỗi máy
            _secretKey = "MyAvaloniaApp_SecretKey_2024_Calendar_Application_JWT_Token_Secret";
            
            // Đường dẫn lưu token
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MyAvaloniaApp");
            Directory.CreateDirectory(appFolder);
            _tokenFilePath = Path.Combine(appFolder, "auth_token.txt");
        }

        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("IsAdmin", user.IsAdmin.ToString()),
                new Claim("IsActive", user.IsActive.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(30), // Token có hiệu lực 30 ngày
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        public void SaveToken(string token)
        {
            try
            {
                File.WriteAllText(_tokenFilePath, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving token: {ex.Message}");
            }
        }

        public string? LoadToken()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    return File.ReadAllText(_tokenFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading token: {ex.Message}");
            }
            return null;
        }

        public void DeleteToken()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    File.Delete(_tokenFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting token: {ex.Message}");
            }
        }

        public User? GetUserFromToken(string token)
        {
            var claims = ValidateToken(token);
            if (claims == null) return null;

            try
            {
                var user = new User
                {
                    Id = int.Parse(claims.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                    Username = claims.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    Role = Enum.Parse<UserRole>(claims.FindFirst(ClaimTypes.Role)?.Value ?? "User"),
                    IsActive = bool.Parse(claims.FindFirst("IsActive")?.Value ?? "false")
                };

                return user;
            }
            catch
            {
                return null;
            }
        }
    }
}
