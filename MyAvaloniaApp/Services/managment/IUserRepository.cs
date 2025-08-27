using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(int userId);
        Task<int> CreateAsync(User user);
        Task UpdateLastLoginAsync(int userId, DateTime lastLoginAt);
        Task<List<User>> GetAllAsync();
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(int userId);
        Task<bool> ResetPasswordAsync(int userId, string newPassword);
        bool ValidatePassword(User user, string password);
    }
}
