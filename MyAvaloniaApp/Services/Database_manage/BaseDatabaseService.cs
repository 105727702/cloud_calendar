using System;
using System.Data;
using System.IO;
using MySql.Data.MySqlClient;

namespace MyAvaloniaApp.Services
{
    public abstract class BaseDatabaseService : IDisposable
    {
        protected readonly string _connectionString;
        private bool _disposed = false;

        protected BaseDatabaseService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        protected MySqlConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        protected void LogError(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                Console.WriteLine($"[ERROR] {message}: {ex.Message}");
            }
            else
            {
                Console.WriteLine($"[ERROR] {message}");
            }
        }

        protected void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {message}");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if needed
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
