using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

class XMPPClient
{
	private const string ServerAddress = "127.0.0.1"; // Адрес сервера
	private const int ServerPort = 5222; // Порт сервера

	private TcpClient client;
	private StreamReader reader;
	private StreamWriter writer;
	private Dictionary<string, List<string>> chatHistory = new(); // История чатов
	private string currentUser = null; // Текущий пользователь
	private Task historyUpdateTask; // Фоновая задача для обновления истории
	private bool isUserTyping = false; // Переменная для отслеживания ввода

	public async Task StartAsync()
	{
		Console.WriteLine("Подключение к серверу...");
		client = new TcpClient();
		try
		{
			await client.ConnectAsync(ServerAddress, ServerPort);
			Console.WriteLine("Подключено к серверу.");

			using var networkStream = client.GetStream();
			reader = new StreamReader(networkStream, Encoding.UTF8);
			writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

			await ShowMenuAsync();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка подключения: {ex.Message}");
			await ReturnToMenuAsync(); 
		}
	}

	private async Task ShowMenuAsync()
	{
		while (true)
		{
			Console.Clear();
			Console.WriteLine("=== Меню XMPP клиента ===");
			if (currentUser == null)
			{
				Console.WriteLine("Вы не авторизованы.");
				Console.WriteLine("1. Авторизация");
				Console.WriteLine("2. Регистрация");
				Console.WriteLine("3. Выход");
			}
			else
			{
				Console.WriteLine($"Вы вошли как: {currentUser}");
				Console.WriteLine("1. Чаты");
				Console.WriteLine("2. Выйти из учетной записи");
			}
			Console.Write("Выберите опцию: ");

			var choice = Console.ReadLine();
			if (currentUser == null)
			{
				switch (choice)
				{
					case "1":
					await AuthenticateAsync();
					break;
					case "2":
					await RegisterAsync();
					break;
					case "3":
					Console.WriteLine("Завершение работы...");
					return;
					default:
					Console.WriteLine("Неверный выбор. Попробуйте снова.");
					break;
				}
			}
			else
			{
				switch (choice)
				{
					case "1":
					await ManageChatsAsync();
					break;
					case "2":
					Console.WriteLine($"Пользователь {currentUser} вышел из системы.");
					currentUser = null;
					break;
					default:
					Console.WriteLine("Неверный выбор. Попробуйте снова.");
					break;
				}
			}
		}
	}

	private async Task AuthenticateAsync()
	{
		try
		{
			Console.Write("Введите имя пользователя: ");
			var username = Console.ReadLine();
			Console.Write("Введите пароль: ");
			var password = Console.ReadLine();

			var authXml = $"<auth username='{username}' password='{password}' />";
			await SendMessageAsync(authXml);

			var response = await reader.ReadLineAsync();
			if (response.Contains("<success/>"))
			{
				currentUser = username;
				Console.WriteLine($"Успешная авторизация! Вы вошли как: {currentUser}.");
			}
			else
			{
				Console.WriteLine("Данные неверные. Авторизация не удалась.");
				Console.WriteLine("Закрытие соединения...");
				CloseConnection();
				Environment.Exit(0); 
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка авторизации: неверные данные. Соеденение закрыто!");
			CloseConnection();
			Environment.Exit(0); 
		}
	}

	private void CloseConnection()
	{
		try
		{
			writer?.Close();
			reader?.Close();
			client?.Close();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка при закрытии соединения: {ex.Message}");
		}
	}

	private async Task RegisterAsync()
	{
		try
		{
			Console.Write("Введите имя пользователя: ");
			var username = Console.ReadLine();
			Console.Write("Введите пароль: ");
			var password = Console.ReadLine();

			var registerXml = $"<register username='{username}' password='{password}' />";
			await SendMessageAsync(registerXml);

			var response = await reader.ReadLineAsync();
			if (response.Contains("<success/>"))
			{
				Console.WriteLine("Успешная регистрация! Теперь вы можете войти.");
			}
			else
			{
				Console.WriteLine("Регистрация не удалась.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка регистрации: {ex.Message}");
		}
	}

	private async Task ManageChatsAsync()
	{
		try
		{
			Console.Clear();
			Console.WriteLine("=== Меню чатов ===");
			Console.WriteLine("Ваши контакты:");
			await LoadContactsAsync();

			Console.WriteLine("Введите имя собеседника для начала общения или /exit для выхода:");
			var contact = Console.ReadLine();
			if (contact == "/exit") return;

			await OpenChatAsync(contact);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка при управлении чатами: {ex.Message}");
		}
	}

	private async Task OpenChatAsync(string contact)
	{
		try
		{
			await LoadChatHistoryAsync(contact);

			Console.Clear();
			Console.WriteLine($"=== Чат с {contact} ===");
			DisplayChatHistory(contact);

			Console.WriteLine("\nВведите сообщение (или /exit для выхода):");

			historyUpdateTask = Task.Run(async () =>
			{
				var lastHistory = new List<string>(chatHistory[contact]);

				while (true)
				{
					await Task.Delay(1000);

					if (isUserTyping)
					{
						continue;
					}

					await LoadChatHistoryAsync(contact);

					if (!chatHistory[contact].SequenceEqual(lastHistory))
					{
						lastHistory = new List<string>(chatHistory[contact]);
						Console.Clear();
						Console.WriteLine($"=== Чат с {contact} ===");
						DisplayChatHistory(contact);
					}
				}
			});

			while (true)
			{
				var message = Console.ReadLine();
				isUserTyping = true;

				if (message == "/exit") break;

				var timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
				var messageXml = $"<message from='{currentUser}' to='{contact}' timestamp='{timestamp}'><body>{message}</body></message>";
				await SendMessageAsync(messageXml);

				var formattedMessage = $"{timestamp} - вы: {message}";
				if (!chatHistory.ContainsKey(contact))
					chatHistory[contact] = new List<string>();

				chatHistory[contact].Add(formattedMessage);

				Console.Clear();
				Console.WriteLine($"=== Чат с {contact} ===");
				DisplayChatHistory(contact);

				isUserTyping = false;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка чата: {ex.Message}");
		}
	}

	private void DisplayChatHistory(string contact)
	{
		Console.WriteLine("\nИстория сообщений:");
		if (chatHistory.TryGetValue(contact, out var messages))
		{
			foreach (var msg in messages)
			{
				Console.WriteLine(msg);
			}
		}
		else
		{
			Console.WriteLine("История сообщений пуста.");
		}
	}

	private async Task LoadContactsAsync()
	{
		try
		{
			var contactsXml = $"<contacts username='{currentUser}' />";
			await SendMessageAsync(contactsXml);

			var response = await reader.ReadLineAsync();
			if (response.StartsWith("<contacts>"))
			{
				ParseContacts(response);
			}
			else
			{
				Console.WriteLine("Не удалось загрузить контакты.");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка загрузки контактов: {ex.Message}");
		}
	}

	private void ParseContacts(string xmlResponse)
	{
		var doc = new XmlDocument();
		doc.LoadXml(xmlResponse);

		foreach (XmlNode contactNode in doc.DocumentElement.SelectNodes("contact"))
		{
			var contactName = contactNode.InnerText;
			Console.WriteLine($"- {contactName}");
		}
	}

	private async Task LoadChatHistoryAsync(string contact)
	{
		try
		{
			var historyXml = $"<loadChatHistory username='{currentUser}' contact='{contact}' />";
			await SendMessageAsync(historyXml);

			var response = await reader.ReadLineAsync();
			if (response.StartsWith("<chatHistory>"))
			{
				ParseChatHistory(contact, response);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка загрузки истории чата: {ex.Message}");
		}
	}

	private void ParseChatHistory(string contact, string xmlResponse)
	{
		var doc = new XmlDocument();
		doc.LoadXml(xmlResponse);

		chatHistory[contact] = new List<string>();
		foreach (XmlNode messageNode in doc.DocumentElement.SelectNodes("message"))
		{
			var from = messageNode.Attributes["from"].Value;
			var to = messageNode.Attributes["to"].Value;
			var timestamp = messageNode.Attributes["timestamp"].Value;
			var body = messageNode.InnerText;

			string formattedMessage;
			if (from == currentUser)
			{
				formattedMessage = $"{timestamp} - вы: {body}";
			}
			else
			{
				formattedMessage = $"{timestamp} - {from}: {body}";
			}

			chatHistory[contact].Add(formattedMessage);
		}
	}

	private async Task SendMessageAsync(string xmlMessage)
	{
		try
		{
			await writer.WriteLineAsync(xmlMessage);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Ошибка при отправке сообщения: {ex.Message}");
		}
	}

	private async Task ReturnToMenuAsync()
	{
		Console.WriteLine("Возвращаемся в меню...");
		await Task.Delay(1);
	}

	static async Task Main()
	{
		var client = new XMPPClient();
		await client.StartAsync();
	}
}
