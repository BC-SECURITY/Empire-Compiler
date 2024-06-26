﻿using EmpireCompiler.Core;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EmpireCompiler
{
    public class Program
    {
        static async Task Main()
        {
            var empireServer = new EmpireService();
            var server = new EmpireServerHandler(empireServer);
            Console.WriteLine("Starting EmpireServer...");
            await server.StartAsync();
        }
    }

    public class EmpireServerHandler
    {
        private readonly EmpireService _service;
        private const string LocalAddress = "127.0.0.1";
        private const int Port = 2012;

        public EmpireServerHandler(EmpireService service)
        {
            _service = service;
        }

        public async Task StartAsync()
        {
            _ = DbInitializer.Initialize(_service);
            var endpoint = new IPEndPoint(IPAddress.Parse(LocalAddress), Port);
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(endpoint);
            listener.Listen(100);

            Console.WriteLine("Listening on {0}:{1}", LocalAddress, Port);
            await AcceptConnectionsAsync(listener);
        }

        private async Task AcceptConnectionsAsync(Socket listener)
        {
            while (true)
            {
                Console.WriteLine("Ready to accept a connection...");
                var socket = await Task.Factory.FromAsync(
                    listener.BeginAccept(null, null),
                    listener.EndAccept);

                Console.WriteLine("Connection accepted from {0}", socket.RemoteEndPoint.ToString());
                if (!await HandleConnectionAsync(socket))
                    break;
            }
        }

        private async Task<bool> HandleConnectionAsync(Socket socket)
        {
            using var memoryStream = new MemoryStream();
            var buffer = new byte[4096];
            Console.WriteLine("Starting data reception...");

            int bytesReceived;
            do
            {
                bytesReceived = await Task<int>.Factory.FromAsync(
                    socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, null, null),
                    socket.EndReceive);

                if (bytesReceived > 0)
                {
                    memoryStream.Write(buffer, 0, bytesReceived);
                    Console.WriteLine("Received {0} bytes", bytesReceived);
                }

                if (bytesReceived < buffer.Length)
                {
                    break;
                }
            }
            while (bytesReceived > 0);

            if (memoryStream.Length == 0)
            {
                Console.WriteLine("No data received. Connection might have been closed by client.");
                return false;
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            string[] message = DecodeMessage(memoryStream.ToArray());
            Console.WriteLine("Received complete message: {0}", string.Join(",", message));

            if (message[0] == "close")
            {
                Console.WriteLine("Received close command. Closing connection.");
                return false;
            }

            await ProcessMessageAsync(socket, message);
            return true;
        }


        private static string[] DecodeMessage(byte[] data)
        {
            var messageData = Encoding.ASCII.GetString(data);
            return messageData.Split(',');
        }

        private async Task ProcessMessageAsync(Socket socket, string[] message)
        {
            try
            {
                Console.WriteLine("Processing message...");
                var tasks = _service.GetEmpire().gruntTasks;
                var taskName = DecodeBase64(message[0]);
                var confuse = DecodeBase64(message[1]) == "true";
                var yaml = DecodeBase64(message[2]);

                _ = DbInitializer.IngestTask(_service, yaml);
                var task = tasks.First(t => t.Name == taskName);
                task.Name = GenerateRandomizedName(task.Name);
                task.Confuse = confuse;
                task.Compile();

                Console.WriteLine("Task compiled successfully as {0}", task.Name);
                await SendResponseAsync(socket, $"FileName:{task.Name}");
            }
            catch (System.Exception ex)
            {
                await SendResponseAsync(socket, "Compile failed");
                Console.WriteLine("Error during message processing: {0}", ex.ToString());
            }
        }

        private static async Task SendResponseAsync(Socket socket, string message)
        {
            var responseBytes = Encoding.ASCII.GetBytes(message);
            await Task.Factory.FromAsync(
                socket.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, null, null),
                socket.EndSend);
            Console.WriteLine("Response sent to client: {0}", message);
        }

        private static string GenerateRandomizedName(string baseName)
        {
            var random = new Random();
            var randomName = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return $"{baseName}_{randomName}";
        }

        private static string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
