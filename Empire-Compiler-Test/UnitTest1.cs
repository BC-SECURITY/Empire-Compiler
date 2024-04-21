using NUnit.Framework;
using Moq;
using System.Threading.Tasks;
using EmpireCompiler.Core;
using System.Net.Sockets;
using System.IO; // For StringWriter
using System.Text; // For encoding byte arrays

namespace EmpireCompiler.Tests
{
    [TestFixture]
    public class EmpireServerHandlerTests
    {
        private Mock<EmpireService> _mockService;
        private Mock<ISocketAdapter> _mockSocketAdapter;
        private EmpireServerHandler _handler;
        private StringWriter _consoleOutput;

        [SetUp]
        public void Setup()
        {
            _mockService = new Mock<EmpireService>();
            _mockSocketAdapter = new Mock<ISocketAdapter>();
            _handler = new EmpireServerHandler(_mockService.Object, _mockSocketAdapter.Object);

            var mockSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _mockSocketAdapter.SetupSequence(x => x.AcceptAsync())
                .ReturnsAsync(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))  // First call returns a socket
                .ReturnsAsync((Socket)null);  // Second call returns null to break the loop

            // Redirect console output
            _consoleOutput = new StringWriter();
            Console.SetOut(_consoleOutput);
        }

        [TearDown]
        public void TearDown()
        {
            // Reset console output to standard output upon test completion
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            _consoleOutput.Dispose();
        }

        [Test, Timeout(5000)]
        public async Task HandleConnectionAsync_ClosesConnectionOnCloseCommand()
        {
            // Act
            await _handler.StartAsync();

            // Assert on the captured output
            string output = _consoleOutput.ToString();
            Assert.That(output, Does.Contain("Ready to accept a connection...")); // Check if specific text was output
        }

        [Test, Timeout(5000)]
        public async Task Server_ClosesConnection_OnCloseCommand()
        {
            // Arrange
            var mockSocket = new Mock<Socket>();
            var buffer = Encoding.ASCII.GetBytes("close");
            var arraySegment = new ArraySegment<byte>(buffer);

            _mockSocketAdapter.Setup(x => x.AcceptAsync()).ReturnsAsync(mockSocket.Object);
            mockSocket.Setup(s => s.ReceiveAsync(arraySegment, SocketFlags.None))
                      .ReturnsAsync(buffer.Length) // Simulate receiving "close" command
                      .Callback(() => _mockSocketAdapter.Setup(x => x.AcceptAsync()).ReturnsAsync((Socket)null)); // Next call to AcceptAsync returns null

            // Act
            await _handler.StartAsync();

            // Assert
            string output = _consoleOutput.ToString();
        }
    }
}