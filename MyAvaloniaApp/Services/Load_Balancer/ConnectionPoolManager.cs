using System;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace MyAvaloniaApp.Services
{
    public class ConnectionPoolManager : IDisposable
    {
        private static ConnectionPoolManager? _instance;
        public static ConnectionPoolManager Instance => _instance ??= new ConnectionPoolManager();

        private readonly ConcurrentQueue<SqliteConnection> _availableConnections;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly string _connectionString;
        private readonly int _maxPoolSize;
        private readonly int _minPoolSize;
        private int _currentPoolSize;
        private bool _disposed;

        private ConnectionPoolManager()
        {
            _maxPoolSize = 20; // Tối đa 20 connections
            _minPoolSize = 5;  // Tối thiểu 5 connections
            _currentPoolSize = 0;
            
            var dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "tasks.db");
            _connectionString = $"Data Source={dbPath}";
            
            _availableConnections = new ConcurrentQueue<SqliteConnection>();
            _connectionSemaphore = new SemaphoreSlim(_maxPoolSize, _maxPoolSize);
            
            // Khởi tạo minimum connections
            Task.Run(InitializeMinimumConnections);
        }

        private async Task InitializeMinimumConnections()
        {
            for (int i = 0; i < _minPoolSize; i++)
            {
                var connection = await CreateNewConnectionAsync();
                if (connection != null)
                {
                    _availableConnections.Enqueue(connection);
                    Interlocked.Increment(ref _currentPoolSize);
                }
            }
        }

        public async Task<SqliteConnection?> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return null;

            await _connectionSemaphore.WaitAsync(cancellationToken);

            try
            {
                // Thử lấy connection từ pool
                if (_availableConnections.TryDequeue(out var connection))
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        return connection;
                    }
                    else
                    {
                        // Connection bị đóng, thử mở lại
                        try
                        {
                            await connection.OpenAsync(cancellationToken);
                            return connection;
                        }
                        catch
                        {
                            connection.Dispose();
                            Interlocked.Decrement(ref _currentPoolSize);
                        }
                    }
                }

                // Tạo connection mới nếu chưa đạt max pool size
                if (_currentPoolSize < _maxPoolSize)
                {
                    var newConnection = await CreateNewConnectionAsync(cancellationToken);
                    if (newConnection != null)
                    {
                        Interlocked.Increment(ref _currentPoolSize);
                        return newConnection;
                    }
                }

                return null;
            }
            catch
            {
                _connectionSemaphore.Release();
                throw;
            }
        }

        public void ReturnConnection(SqliteConnection connection)
        {
            if (_disposed || connection == null)
            {
                _connectionSemaphore.Release();
                return;
            }

            try
            {
                if (connection.State == ConnectionState.Open && _currentPoolSize <= _maxPoolSize)
                {
                    _availableConnections.Enqueue(connection);
                }
                else
                {
                    connection.Dispose();
                    Interlocked.Decrement(ref _currentPoolSize);
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        private async Task<SqliteConnection?> CreateNewConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Đóng tất cả connections
            while (_availableConnections.TryDequeue(out var connection))
            {
                connection?.Dispose();
            }

            _connectionSemaphore?.Dispose();
        }
    }
}
