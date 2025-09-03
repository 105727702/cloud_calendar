using System;

namespace MyAvaloniaApp.Services
{
    public class DatabaseConfiguration
    {
        public static class MySQL
        {
            // Thông tin kết nối MySQL - Điều chỉnh theo thiết lập của bạn
            public static string Server { get; set; } = "localhost";
            public static string Port { get; set; } = "3306";
            public static string Database { get; set; } = "cloud_calendar";
            public static string Username { get; set; } = "root";
            public static string Password { get; set; } = "Emdeptrai17012006#"; // Điền password MySQL của bạn
            
            public static string ConnectionString => 
                $"Server={Server};Port={Port};Database={Database};Uid={Username};Pwd={Password};CharSet=utf8mb4;Convert Zero Datetime=True;";
        }
        
        // Backup SQLite connection string (nếu cần)
        public static class SQLite
        {
            public static string ConnectionString { get; set; } = "Data Source=tasks.db";
        }
    }
}
