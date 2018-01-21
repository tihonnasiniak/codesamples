using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NMEP;
using NUnit.Framework;
namespace NMEPTests
{
    [TestFixture]
    class ServerTests
    {
        public int FirstConnectionCallbackCalls;
        public int FirstMessageCallbackCalls;
        public int SecondMessageCallbackCalls;
        public Server TestServer;
        [Test]
        public void RunServer_ConnectClient_CallbackCalled()
        {
            FirstConnectionCallbackCalls = 0;
            Server TestServer = new Server(701);
            TestServer.ClientConnected += FirstConnectionCallback;
            TestServer.Start();
            Client TestClient = new Client(702);
            TestClient.Start("127.0.0.1", 701);
            Task.Delay(1000).Wait();
            TestClient.Stop();
            TestServer.Stop();
            Assert.That(FirstConnectionCallbackCalls, Is.EqualTo(1));
        }
        public void FirstConnectionCallback(ConnectedClient iClient)
        {
            ++FirstConnectionCallbackCalls;
            Assert.That(iClient.BytesPerChunk == Consts.BYTES_PER_CHUNK);
        }
        [Test]
        public void SendMessage_CorrectAnswer()
        {
            FirstMessageCallbackCalls = 0;
            TestServer = new Server(703);
            TestServer.MessageReceived += FirstMessageCallback;
            TestServer.Start();
            Client TestClient = new Client(704);
            TestClient.Start("127.0.0.1", 703);
            var TaskToWait = TestClient.SendMessageAsync(Encoding.Unicode.GetBytes("A"));
            TaskToWait.Wait();
            Assert.That(Encoding.Unicode.GetString(TaskToWait.Result.ContainedMessage), Is.EqualTo("B"));
            Task.Delay(1000).Wait();
            TestClient.Stop();
            TestServer.Stop();
            Assert.That(FirstMessageCallbackCalls, Is.EqualTo(1));
        }
        public void FirstMessageCallback(ConnectedClient iClient, Message iMessage)
        {
            ++FirstMessageCallbackCalls;
            Assert.That(Encoding.Unicode.GetString(iMessage.ContainedMessage), Is.EqualTo("A"));
            TestServer.SendMessageAsync(Encoding.Unicode.GetBytes("B"), iClient, iMessage.MessageID).Wait();
        }
        [Test]
        public void SendMessage_LongMessage_CorrectAnswer()
        {
            SecondMessageCallbackCalls = 0;
            TestServer = new Server(705);
            TestServer.MessageReceived += SecondMessageCallback;
            TestServer.Start();
            Client TestClient = new Client(706);
            TestClient.Start("127.0.0.1", 705);
            var TaskToWait = TestClient.SendMessageAsync(new byte[Consts.BYTES_PER_CHUNK * 2]);
            TaskToWait.Wait();
            Assert.That(TaskToWait.Result.ContainedMessage.Length, Is.EqualTo(Consts.BYTES_PER_CHUNK * 3));
            Task.Delay(1000).Wait();
            TestClient.Stop();
            TestServer.Stop();
            Assert.That(SecondMessageCallbackCalls, Is.EqualTo(1));
        }
        public void SecondMessageCallback(ConnectedClient iClient, Message iMessage)
        {
            ++SecondMessageCallbackCalls;
            Assert.That(iMessage.ContainedMessage.Length, Is.EqualTo(Consts.BYTES_PER_CHUNK * 2));
            TestServer.SendMessageAsync(new byte[Consts.BYTES_PER_CHUNK * 3], iClient, iMessage.MessageID).Wait();
        }
        [Test]
        public void Connect_IncorrectHandshake()
        {
            Server TestSever = new Server(707);
            TestSever.Start();
            Socket TestSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TestSocket.Bind(new IPEndPoint(IPAddress.Any, 708));
            TestSocket.Connect(IPAddress.Parse("127.0.0.1"), 707);
            List<byte> HandshakeBytes = new List<byte>();
            HandshakeBytes.Add(Consts.HANDSHAKE_SIGN);
            HandshakeBytes.Add(Consts.PROTOCOL_VERSION + 10);
            HandshakeBytes.AddRange(BitConverter.GetBytes(Consts.BYTES_PER_CHUNK));
            TestSocket.Send(HandshakeBytes.ToArray());
            TestSocket.Close(1);
            TestSever.Stop();
        }
        public delegate void TestDelegate();
        [Test]
        public void ConnectedClientRecieveData_ResultIsNotBuffer_Exception()
        {
            Server TestServer = new Server(887);
            TestServer.Start();
            TestDelegate Delegate = () => { };
            Socket TestSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            TestSocket.Bind(new IPEndPoint(IPAddress.Any, 888));
            TestSocket.Connect(IPAddress.Parse("127.0.0.1"), 887);
            ConnectedClient TestClient = new ConnectedClient(999, 1024, TestSocket);
            Assert.That(() => TestClient.ReceiveData(Delegate.BeginInvoke(null, "A")), Throws.Exception);
        }
    }
}
