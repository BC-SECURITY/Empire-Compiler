using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EmpireCompiler.Core;

namespace EmpireCompiler
{
    public class Program
    {
        static async Task Main()
        {
            var empireServer = new EmpireService();
            var server = new EmpireServerHandler(empireServer);
            await server.StartAsync();
        }
    }

    public class EmpireServerHandler
    {
        private readonly EmpireService _service;
        private const string LocalAddress = "127.0.0.1";
        private const int Port = 2012;
        private const int BufferSize = 10000000; // Consider adjusting or using a MemoryStream if size varies

        public EmpireServerHandler(EmpireService service)
        {
            _service = service;
        }

        private string GenerateRandomizedName(string baseName)
        {
            var random = new Random();
            var randomName = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return $"{baseName}_{randomName}";
        }

        private string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
        private string[] DecodeMessage(byte[] buffer, int bytesReceived)
        {
            var data = Encoding.ASCII.GetString(buffer, 0, bytesReceived);
            return data.Split(',');
        }

        public async Task StartAsync()
        {
            DbInitializer.Initialize(_service);
            var endpoint = new IPEndPoint(IPAddress.Parse(LocalAddress), Port);
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(endpoint);
            listener.Listen(100);

            await AcceptConnectionsAsync(listener);
        }

        private async Task AcceptConnectionsAsync(Socket listener)
        {
            while (true)
            {
                Console.WriteLine("Compiler ready");
                var socket = await Task.Factory.FromAsync(
                    listener.BeginAccept(null, null),
                    listener.EndAccept);

                if (!await HandleConnectionAsync(socket))
                    break;
            }
        }

        private async Task<bool> HandleConnectionAsync(Socket socket)
        {
            var buffer = new byte[BufferSize];
            var bytesReceived = await Task<int>.Factory.FromAsync(
                socket.BeginReceive(buffer, 0, BufferSize, SocketFlags.None, null, null),
                socket.EndReceive);

            string[] message = DecodeMessage(buffer, bytesReceived);
            if (message[0] == "close")
                return false;

            await ProcessMessageAsync(socket, message);
            return true;
        }

        private async Task ProcessMessageAsync(Socket socket, string[] message)
        {
            try
            {
                var tasks = _service.GetEmpire().gruntTasks;
                var taskName = DecodeBase64(message[0]);
                var confuse = DecodeBase64(message[1]) == "true";
                var yaml = DecodeBase64(message[2]);

                DbInitializer.IngestTask(_service, yaml);
                var task = tasks.First(t => t.Name == taskName);
                task.Name = GenerateRandomizedName(task.Name);
                task.Confuse = confuse;
                task.Compile();

                await SendResponseAsync(socket, $"FileName:{task.Name}");
            }
            catch (System.Exception ex)
            {
                await SendResponseAsync(socket, "Compile failed");
                Console.WriteLine(ex.ToString());
            }
        }

        private async Task SendResponseAsync(Socket socket, string message)
        {
            var responseBytes = Encoding.ASCII.GetBytes(message);
            await Task.Factory.FromAsync(
                socket.BeginSend(responseBytes, 0, responseBytes.Length, SocketFlags.None, null, null),
                socket.EndSend);
        }
    }
}
