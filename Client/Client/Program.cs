﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleApp2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TcpClient tcpClient = new TcpClient("34.116.232.242", 25565);
            NetworkStream stream = tcpClient.GetStream();

            byte[] idBuffer = new byte[10];
            int idBytesRead = stream.Read(idBuffer, 0, idBuffer.Length);
            string clientId = Encoding.ASCII.GetString(idBuffer, 0, idBytesRead).Trim();

            Console.WriteLine($"Connected to the server as Client {clientId}");

            Thread readThread = new Thread(() =>
            {
                byte[] buffer = new byte[100];
                while (true)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            Console.WriteLine(message);
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Connection lost.");
                        break;
                    }
                }
            });

            readThread.Start();

            while (true)
            {
                Console.WriteLine("1 - Write to server a message (100 bytes)\n2 - Exit");
                int choice = int.Parse(Console.ReadLine());

                if (choice == 2)
                {
                    tcpClient.Close();
                    break;
                }

                Console.Write("Your message: ");
                string message = Console.ReadLine();
                byte[] messageBytes = Encoding.ASCII.GetBytes(message);
                stream.Write(messageBytes, 0, messageBytes.Length);
                Console.WriteLine($"Sent message to server as Client {clientId}");
            }
        }
    }
}