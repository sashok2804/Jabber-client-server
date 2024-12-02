using System;
using System.Text;
using System.Xml;
using System.Net.Sockets;
using System.IO;
using Serilog;

public static class XMPPHandlers
{
	public static string ProcessXml(string xml, TcpClient client, StreamWriter writer)
	{
		try
		{
			var doc = new XmlDocument();
			doc.LoadXml(xml);
			var response = doc.DocumentElement.LocalName switch
			{
				"message" => HandleMessage(doc),
				"presence" => HandlePresence(doc),
				"auth" => HandleAuth(doc, client, writer),
				"register" => RegisterUser(doc),
				"contacts" => GetUserContacts(doc),
				"search" => HandleSearch(doc),
				"loadChatHistory" => LoadChatHistory(doc),
				_ => null
			};

			if (response == null)
			{
				Log.Warning($"Неизвестный тип XML-команды: {doc.DocumentElement.LocalName}");
			}
			return response;
		}
		catch (XmlException ex)
		{
			Log.Error(ex, "Неверный XML.");
			return "<failure>Invalid XML</failure>";
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка обработки XML.");
			return "<failure>Server error</failure>";
		}
	}

	public static string HandleMessage(XmlDocument doc)
	{
		var from = doc.DocumentElement.GetAttribute("from");
		var to = doc.DocumentElement.GetAttribute("to");
		var body = doc.DocumentElement.SelectSingleNode("body")?.InnerText;

		if (string.IsNullOrEmpty(body))
		{
			Log.Warning($"Сообщение от {from} для {to} пустое.");
			return "<failure>Empty message</failure>";
		}

		DatabaseHelper.SaveMessage(from, to, body);
		Log.Information($"Сообщение от {from} для {to}: {body}");
		XMPPServer.SendMessageToClient(to, $"<message from='{from}' to='{to}'><body>{body}</body></message>");
		return $"<message from='{to}' to='{from}'><body>Сообщение получено.</body></message>";
	}

	public static string HandlePresence(XmlDocument doc)
	{
		var from = doc.DocumentElement.GetAttribute("from");
		var status = doc.DocumentElement.SelectSingleNode("status")?.InnerText;

		if (string.IsNullOrEmpty(from))
		{
			Log.Warning("Поле 'from' в сообщении присутствия отсутствует.");
			return "<failure>Invalid presence</failure>";
		}

		DatabaseHelper.UpdatePresence(from, status);
		Log.Information($"Присутствие от {from}: {status}");
		return "<presence type='available' />";
	}

	public static string HandleAuth(XmlDocument doc, TcpClient client, StreamWriter writer)
	{
		var username = doc.DocumentElement.GetAttribute("username");
		var password = doc.DocumentElement.GetAttribute("password");

		if (DatabaseHelper.AuthenticateUser(username, password))
		{
			XMPPServer.RegisterClient(username, writer);
			Log.Information($"Успешная авторизация {username}.");
			return "<success/>";
		}

		Log.Warning($"Неудачная авторизация {username}. Соединение будет закрыто.");
		client.Close();
		return "<failure>Authentication failed</failure>";
	}

	public static string RegisterUser(XmlDocument doc)
	{
		var username = doc.DocumentElement.GetAttribute("username");
		var password = doc.DocumentElement.GetAttribute("password");

		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
		{
			Log.Warning("Поля username или password отсутствуют в запросе на регистрацию.");
			return "<failure>Invalid registration data</failure>";
		}

		if (DatabaseHelper.AddUser(username, password))
		{
			Log.Information($"Пользователь {username} успешно зарегистрирован.");
			return "<success/>";
		}

		Log.Warning($"Ошибка регистрации пользователя {username}. Возможно, пользователь уже существует.");
		return "<failure>User already exists</failure>";
	}

	public static string GetUserContacts(XmlDocument doc)
	{
		var username = doc.DocumentElement.GetAttribute("username");

		if (string.IsNullOrEmpty(username))
		{
			Log.Warning("Поле 'username' отсутствует в запросе на получение контактов.");
			return "<failure>Invalid username</failure>";
		}

		var contacts = DatabaseHelper.GetUserContacts(username);
		var response = new StringBuilder("<contacts>");
		foreach (var contact in contacts)
		{
			response.Append($"<contact>{contact}</contact>");
		}
		response.Append("</contacts>");
		return response.ToString();
	}

	public static string HandleSearch(XmlDocument doc)
	{
		var usernameToSearch = doc.DocumentElement.GetAttribute("username");

		if (string.IsNullOrEmpty(usernameToSearch))
		{
			Log.Warning("Поле 'username' отсутствует в запросе на поиск.");
			return "<failure>Invalid search data</failure>";
		}

		var status = DatabaseHelper.UserExists(usernameToSearch) ? "found" : "not_found";
		Log.Information($"Результаты поиска пользователя {usernameToSearch}: {status}");
		return $"<search><user>{usernameToSearch}</user><status>{status}</status></search>";
	}

	public static string LoadChatHistory(XmlDocument doc)
	{
		var username = doc.DocumentElement.GetAttribute("username");
		var contact = doc.DocumentElement.GetAttribute("contact");

		if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(contact))
		{
			Log.Warning("Поля username или contact отсутствуют в запросе на загрузку истории чата.");
			return "<failure>Invalid chat history request</failure>";
		}

		var messages = DatabaseHelper.GetChatHistory(username, contact);
		var response = new StringBuilder("<chatHistory>");
		foreach (var (from, to, body, timestamp) in messages)
		{
			response.Append($"<message from='{from}' to='{to}' timestamp='{timestamp}'>{body}</message>");
		}
		response.Append("</chatHistory>");
		return response.ToString();
	}
}
