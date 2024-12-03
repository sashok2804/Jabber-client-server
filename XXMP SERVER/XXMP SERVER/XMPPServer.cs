using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

public class XMPPServer
{
	private const int Port = 5222;
	private static readonly Dictionary<string, StreamWriter> ActiveClients = new();
	private CancellationTokenSource _cts;

	public async Task StartAsync()
	{
		LogHelper.ConfigureLogging();
		var listener = new TcpListener(IPAddress.Any, Port);
		_cts = new CancellationTokenSource();

		try
		{
			listener.Start();
			Log.Information($"XMPP сервер запущен на порту {Port}");

			while (!_cts.Token.IsCancellationRequested)
			{
				try
				{
					var client = await listener.AcceptTcpClientAsync();
					Log.Information("Новое соединение от клиента {0}.", client.Client.RemoteEndPoint);
					_ = HandleClientAsync(client);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Ошибка при принятии нового соединения.");
				}
			}
		}
		finally
		{
			listener.Stop();
			Log.Information("Сервер остановлен.");
		}
	}

	public void Stop()
	{
		_cts?.Cancel();
		Log.Information("Остановка сервера...");
	}

	private async Task HandleClientAsync(TcpClient client)
	{
		NetworkStream stream = null;
		StreamReader reader = null;
		StreamWriter writer = null;

		try
		{
			stream = client.GetStream();
			reader = new StreamReader(stream, Encoding.UTF8);
			writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

			while (client.Connected)
			{
				string request;
				try
				{
					request = await reader.ReadLineAsync();
				}
				catch (IOException)
				{
					Log.Warning("Соединение с клиентом {0} разорвано.", client.Client.RemoteEndPoint);
					break;
				}

				if (string.IsNullOrEmpty(request))
				{
					Log.Information("Клиент {0} завершил соединение.", client.Client.RemoteEndPoint);
					break;
				}

				
				if (!request.Contains("<loadChatHistory"))
				{
					Log.Information($"Получен запрос от клиента {client.Client.RemoteEndPoint}: {request}"); 
				}
				var response = XMPPHandlers.ProcessXml(request, client, writer);
				if (response != null)
				{
					try
					{
						await writer.WriteLineAsync(response);
					}
					catch (IOException ex)
					{
						Log.Warning(ex, "Ошибка при отправке данных клиенту {0}.", client.Client.RemoteEndPoint);
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Ошибка обработки клиента {0}.", client.Client.RemoteEndPoint);
		}
		finally
		{
			writer?.Dispose();
			reader?.Dispose();
			stream?.Dispose();
			client.Close();
			Log.Information($"Соединение с клиентом {client.Client.RemoteEndPoint} закрыто.");
		}
	}

	public static void RegisterClient(string username, StreamWriter writer) => ActiveClients[username] = writer;

	public static void UnregisterClient(string username) => ActiveClients.Remove(username);

	public static void SendMessageToClient(string username, string message)
	{
		if (ActiveClients.TryGetValue(username, out var writer))
		{
			try
			{
				writer.WriteLine(message);
			}
			catch (IOException ex)
			{
				Log.Warning(ex, "Ошибка при отправке сообщения клиенту {0}.", username);
			}
		}
	}

	public static async Task Main(string[] args)
	{
		var server = new XMPPServer();
		await server.StartAsync();
	}
}
