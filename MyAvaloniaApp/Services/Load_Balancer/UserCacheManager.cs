using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyAvaloniaApp.Models;

namespace MyAvaloniaApp.Services
{
    public class UserCacheManager
    {
        private static UserCacheManager? _instance;
        public static UserCacheManager Instance => _instance ??= new UserCacheManager();

        private readonly ConcurrentDictionary<string, CachedUser> _userCache;
        private readonly ConcurrentDictionary<int, CachedUser> _userIdCache;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cacheExpiry;

        private UserCacheManager()
        {
            _userCache = new ConcurrentDictionary<string, CachedUser>();
            _userIdCache = new ConcurrentDictionary<int, CachedUser>();
            _cacheExpiry = TimeSpan.FromMinutes(30); // Cache trong 30 phút
            
            // Cleanup expired items every 10 minutes
            _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        }

        public void CacheUser(User user)
        {
            if (user == null) return;

            var cachedUser = new CachedUser
            {
                User = user,
                CachedAt = DateTime.UtcNow
            };

            _userCache.AddOrUpdate(user.Username.ToLower(), cachedUser, (key, old) => cachedUser);
            _userIdCache.AddOrUpdate(user.Id, cachedUser, (key, old) => cachedUser);
        }

        public User? GetCachedUserByUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;

            if (_userCache.TryGetValue(username.ToLower(), out var cachedUser))
            {
                if (DateTime.UtcNow - cachedUser.CachedAt < _cacheExpiry)
                {
                    return cachedUser.User;
                }
                else
                {
                    // Expired, remove from cache
                    _userCache.TryRemove(username.ToLower(), out _);
                    _userIdCache.TryRemove(cachedUser.User.Id, out _);
                }
            }

            return null;
        }

        public User? GetCachedUserById(int userId)
        {
            if (_userIdCache.TryGetValue(userId, out var cachedUser))
            {
                if (DateTime.UtcNow - cachedUser.CachedAt < _cacheExpiry)
                {
                    return cachedUser.User;
                }
                else
                {
                    // Expired, remove from cache
                    _userIdCache.TryRemove(userId, out _);
                    _userCache.TryRemove(cachedUser.User.Username.ToLower(), out _);
                }
            }

            return null;
        }

        public void InvalidateUser(string username)
        {
            if (string.IsNullOrEmpty(username)) return;

            if (_userCache.TryRemove(username.ToLower(), out var cachedUser))
            {
                _userIdCache.TryRemove(cachedUser.User.Id, out _);
            }
        }

        public void InvalidateUser(int userId)
        {
            if (_userIdCache.TryRemove(userId, out var cachedUser))
            {
                _userCache.TryRemove(cachedUser.User.Username.ToLower(), out _);
            }
        }

        public void ClearCache()
        {
            _userCache.Clear();
            _userIdCache.Clear();
        }

        private void CleanupExpiredItems(object? state)
        {
            var expiredItems = new List<string>();
            var expiredIds = new List<int>();

            foreach (var kvp in _userCache)
            {
                if (DateTime.UtcNow - kvp.Value.CachedAt >= _cacheExpiry)
                {
                    expiredItems.Add(kvp.Key);
                    expiredIds.Add(kvp.Value.User.Id);
                }
            }

            foreach (var item in expiredItems)
            {
                _userCache.TryRemove(item, out _);
            }

            foreach (var id in expiredIds)
            {
                _userIdCache.TryRemove(id, out _);
            }

            if (expiredItems.Count > 0)
            {
                Console.WriteLine($"Cleaned up {expiredItems.Count} expired cache items");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _userCache.Clear();
            _userIdCache.Clear();
        }
    }

    public class CachedUser
    {
        public User User { get; set; } = null!;
        public DateTime CachedAt { get; set; }
    }
}
