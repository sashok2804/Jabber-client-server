using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using Serilog;

public static class DatabaseHelper
{
	private const string ConnectionString = "Data Source=chatapp.db;";

	private static void EnsureDatabaseExists()
	{
		if (!System.IO.File.Exists("chatapp.db"))
		{
			Log.Information("База данных не найдена. Создаётся новая база данных...");
			using (var connection = new SqliteConnection(ConnectionString))
			{
				connection.Open();
				connection.Close();
			}

			ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Username TEXT PRIMARY KEY, 
                    PasswordHash TEXT, 
                    PresenceStatus TEXT
                );
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    FromUsername TEXT, 
                    ToUsername TEXT, 
                    Body TEXT, 
                    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                );");

			Log.Information("База данных и таблицы успешно созданы.");
		}
	}

	public static bool AddUser(string username, string password)
	{
		EnsureDatabaseExists();
		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;

		var hashedPassword = HashPassword(password);
		try
		{
			ExecuteNonQuery("INSERT INTO Users (Username, PasswordHash) VALUES (@username, @password)",
				("@username", username), ("@password", hashedPassword));
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка при добавлении пользователя.");
			return false;
		}
	}

	public static bool AuthenticateUser(string username, string password)
	{
		EnsureDatabaseExists();
		var storedHash = ExecuteScalar<string>("SELECT PasswordHash FROM Users WHERE Username = @username", ("@username", username));
		if (storedHash == null) return false;

		return storedHash == HashPassword(password);
	}

	public static void SaveMessage(string from, string to, string body)
	{
		EnsureDatabaseExists();
		ExecuteNonQuery("INSERT INTO Messages (FromUsername, ToUsername, Body) VALUES (@from, @to, @body)",
			("@from", from), ("@to", to), ("@body", body));
	}

	public static void UpdatePresence(string username, string status)
	{
		EnsureDatabaseExists();
		ExecuteNonQuery("UPDATE Users SET PresenceStatus = @status WHERE Username = @username",
			("@username", username), ("@status", status));
	}

	public static List<(string, string, string, DateTime)> GetChatHistory(string username, string contact)
	{
		EnsureDatabaseExists();
		var query = @"
            SELECT FromUsername, ToUsername, Body, Timestamp
            FROM Messages
            WHERE (FromUsername = @username AND ToUsername = @contact) 
                OR (FromUsername = @contact AND ToUsername = @username)
            ORDER BY Timestamp";
		var messages = new List<(string, string, string, DateTime)>();
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		command.Parameters.AddWithValue("@username", username);
		command.Parameters.AddWithValue("@contact", contact);
		connection.Open();
		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			messages.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
		}
		return messages;
	}

	public static List<string> GetUserContacts(string username)
	{
		EnsureDatabaseExists();
		var query = @"
            SELECT DISTINCT CASE WHEN FromUsername = @username THEN ToUsername ELSE FromUsername END 
            FROM Messages
            WHERE FromUsername = @username OR ToUsername = @username";
		var contacts = new List<string>();
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		command.Parameters.AddWithValue("@username", username);
		connection.Open();
		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			contacts.Add(reader.GetString(0));
		}
		return contacts;
	}

	public static bool UserExists(string username)
	{
		EnsureDatabaseExists();
		return ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username = @username", ("@username", username)) > 0;
	}

	private static void ExecuteNonQuery(string query, params (string, object)[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		foreach (var (param, value) in parameters)
		{
			command.Parameters.AddWithValue(param, value);
		}
		connection.Open();
		command.ExecuteNonQuery();
	}

	private static T ExecuteScalar<T>(string query, params (string, object)[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		foreach (var (param, value) in parameters)
		{
			command.Parameters.AddWithValue(param, value);
		}
		connection.Open();
		return (T)command.ExecuteScalar();
	}

	private static string HashPassword(string password)
	{
		using var sha256 = SHA256.Create();
		var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
		return Convert.ToBase64String(hashBytes);
	}
}
