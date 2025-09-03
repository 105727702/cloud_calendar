using System;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace MyAvaloniaApp.Services
{
    public class DatabaseConfiguration
    {
        private static IConfiguration? _configuration;
        
        static DatabaseConfiguration()
        {
            LoadConfiguration();
        }
        
        private static void LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
                
            _configuration = builder.Build();
        }

        public static class MySQL
        {
            // Đọc từ appsettings.json hoặc Environment Variables
            public static string Server => GetConfigValue("Database:MySQL:Server", "localhost");
            public static string Port => GetConfigValue("Database:MySQL:Port", "3306");
            public static string Database => GetConfigValue("Database:MySQL:Database", "cloud_calendar");
            public static string Username => GetConfigValue("Database:MySQL:Username", "root");
            public static string Password => GetConfigValue("Database:MySQL:Password", "");
            
            public static string ConnectionString => 
                $"Server={Server};Port={Port};Database={Database};Uid={Username};Pwd={Password};CharSet=utf8mb4;Convert Zero Datetime=True;";
        }
        
        public static class Jwt
        {
            public static string SecretKey => GetConfigValue("Jwt:SecretKey", "default-secret-key-change-this");
            public static string Issuer => GetConfigValue("Jwt:Issuer", "CloudCalendarApp");
            public static string Audience => GetConfigValue("Jwt:Audience", "CloudCalendarUsers");
        }
        
        // Backup SQLite connection string (nếu cần)
        public static class SQLite
        {
            public static string ConnectionString => GetConfigValue("Database:SQLite:ConnectionString", "Data Source=tasks.db");
        }
        
        private static string GetConfigValue(string key, string defaultValue)
        {
            // Thử đọc từ Environment Variable trước (cao nhất priority)
            var envValue = Environment.GetEnvironmentVariable(key.Replace(":", "_").ToUpper());
            if (!string.IsNullOrEmpty(envValue))
                return envValue;
            
            // Sau đó đọc từ configuration file
            return _configuration?[key] ?? defaultValue;
        }
    }
}
