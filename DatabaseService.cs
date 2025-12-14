using System;
using System.Data.SQLite;
using System.IO;

namespace MafiaRegistrationBot
{
    public class DatabaseService
    {
        private string _databasePath;

        public DatabaseService(string databaseName = "MafiaGame.sqlite")
        {
            _databasePath = $"Data Source={databaseName};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists("MafiaGame.sqlite"))
            {
                SQLiteConnection.CreateFile("MafiaGame.sqlite");
            }

            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.Open();
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Players (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TelegramUserId INTEGER UNIQUE NOT NULL,
                        FirstName TEXT NOT NULL,
                        LastName TEXT NULL,
                        UserName TEXT NULL,
                        GroupNumber TEXT NOT NULL,
                        FullName TEXT NOT NULL,
                        RegistrationDate TEXT NOT NULL
                    )";
                var command = new SQLiteCommand(createTableQuery, connection);
                command.ExecuteNonQuery();
            }
        }

        public bool RegisterPlayer(Player player)
        {
            try
            {
                using (var connection = new SQLiteConnection(_databasePath))
                {
                    connection.Open();
                    string insertQuery = @"
                        INSERT INTO Players 
                        (TelegramUserId, FirstName, LastName, UserName, GroupNumber, FullName, RegistrationDate)
                        VALUES 
                        (@userId, @firstName, @lastName, @userName, @groupNumber, @fullName, @regDate)";

                    var command = new SQLiteCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@userId", player.TelegramUserId);
                    command.Parameters.AddWithValue("@firstName", player.FirstName);
                    command.Parameters.AddWithValue("@lastName", player.LastName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@userName", player.UserName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@groupNumber", player.GroupNumber);
                    command.Parameters.AddWithValue("@fullName", player.FullName);
                    command.Parameters.AddWithValue("@regDate", player.RegistrationDate.ToString("yyyy-MM-dd HH:mm:ss"));

                    int rowsAffected = command.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine($"Ошибка при регистрации игрока: {ex.Message}");
                return false;
            }
        }

        public bool IsPlayerRegistered(long telegramUserId)
        {
            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM Players WHERE TelegramUserId = @userId";
                var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@userId", telegramUserId);

                int count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        public List<Player> GetAllPlayers()
        {
            var players = new List<Player>();

            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.Open();
                string query = "SELECT * FROM Players ORDER BY RegistrationDate DESC";
                var command = new SQLiteCommand(query, connection);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        players.Add(new Player
                        {
                            TelegramUserId = Convert.ToInt64(reader["TelegramUserId"]),
                            FirstName = reader["FirstName"].ToString(),
                            LastName = reader["LastName"]?.ToString(),
                            UserName = reader["UserName"]?.ToString(),
                            GroupNumber = reader["GroupNumber"].ToString(),
                            FullName = reader["FullName"].ToString(),
                            RegistrationDate = DateTime.Parse(reader["RegistrationDate"].ToString())
                        });
                    }
                }
            }

            return players;
        }

        public bool UnregisterPlayer(long telegramUserId)
        {
            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.Open();
                string query = "DELETE FROM Players WHERE TelegramUserId = @userId";
                var command = new SQLiteCommand(query, connection);
                command.Parameters.AddWithValue("@userId", telegramUserId);

                int rowsAffected = command.ExecuteNonQuery();
                return rowsAffected > 0;
            }
        }

        public int GetRegisteredPlayersCount()
        {
            using (var connection = new SQLiteConnection(_databasePath))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM Players";
                var command = new SQLiteCommand(query, connection);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }
    }

    public class Player
    {
        public long TelegramUserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string GroupNumber { get; set; }
        public string FullName { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
}