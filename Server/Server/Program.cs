using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;  

namespace ConsoleApp1
{
    internal class Program
    {
        private static List<(TcpClient Client, int Id)> _clients = new List<(TcpClient, int)>();
        static string connectionString = "Server=34.116.232.242;Database=mydatabase;User ID=myuser;Password=mypassword;";
        private static int _nextClientId = 1;

        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.Any, 25565);
            server.Start();
            Console.WriteLine("Server started");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                int clientId;
                lock (_clients)
                {
                    clientId = _nextClientId++;
                    _clients.Add((client, clientId));
                }
                Console.WriteLine($"Client {clientId} connected!");

                // Логування підключення до бази даних
                LogConnection(clientId);

                NetworkStream stream = client.GetStream();
                byte[] idMessage = Encoding.ASCII.GetBytes(clientId.ToString());
                stream.Write(idMessage, 0, idMessage.Length);

                Thread clientThread = new Thread(() => HandleClient(client, clientId));
                clientThread.Start();
            }
        }

        private static void HandleClient(TcpClient client, int clientId)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[100];

            while (client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Client {clientId} sent message: {message}");

                    // Логування повідомлення до бази даних
                    LogMessage(clientId, message);

                    BroadcastMessage(clientId, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error from Client {clientId}: " + ex.Message);
                    break;
                }
            }

            lock (_clients)
            {
                _clients.RemoveAll(c => c.Client == client);
            }

            client.Close();
            Console.WriteLine($"Client {clientId} disconnected");

            // Логування відключення до бази даних
            LogDisconnection(clientId);
        }

        private static void BroadcastMessage(int senderId, string message)
        {
            byte[] messageBytes = Encoding.ASCII.GetBytes($"Client {senderId}: {message}");
            List<TcpClient> disconnectedClients = new List<TcpClient>();

            lock (_clients)
            {
                foreach (var (client, _) in _clients)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(messageBytes, 0, messageBytes.Length);
                    }
                    catch (IOException ioEx)
                    {
                        Console.WriteLine($"IOException occurred while broadcasting message: {ioEx.Message}");
                        disconnectedClients.Add(client);
                    }
                    catch (ObjectDisposedException objEx)
                    {
                        Console.WriteLine($"ObjectDisposedException occurred: {objEx.Message}");
                        disconnectedClients.Add(client);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Unexpected error occurred: {ex.Message}");
                        disconnectedClients.Add(client);
                    }
                }

                foreach (TcpClient disconnectedClient in disconnectedClients)
                {
                    lock (_clients)
                    {
                        if (_clients.Exists(c => c.Client == disconnectedClient))
                        {
                            _clients.RemoveAll(c => c.Client == disconnectedClient);
                            disconnectedClient.Close();
                        }
                    }
                }
            }
        }

        // Функція для логування підключення до бази даних
        private static void LogConnection(int clientId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO connections (ClientId, ConnectionTime) VALUES (@ClientId, @ConnectionTime)";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ClientId", clientId);
                    command.Parameters.AddWithValue("@ConnectionTime", DateTime.Now);
                    command.ExecuteNonQuery();
                }
            }
        }

        // Функція для логування повідомлень до бази даних
        private static void LogMessage(int clientId, string message)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO messages (ClientId, Message, MessageTime) VALUES (@ClientId, @Message, @MessageTime)";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ClientId", clientId);
                    command.Parameters.AddWithValue("@Message", message);
                    command.Parameters.AddWithValue("@MessageTime", DateTime.Now);
                    command.ExecuteNonQuery();
                }
            }
        }

        // Функція для логування відключення до бази даних
        private static void LogDisconnection(int clientId)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string query = "UPDATE connections SET DisconnectionTime = @DisconnectionTime WHERE ClientId = @ClientId";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ClientId", clientId);
                    command.Parameters.AddWithValue("@DisconnectionTime", DateTime.Now);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
