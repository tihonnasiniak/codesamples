using System;
//using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using NMEP;
using NUnit.Framework;
namespace NMEPTests
{
    [TestFixture]
    public class ClientTests : Client
    {
        [Test]
        public void SendMessage_AddChunk_TaskEnded()
        {
            Server TestServer = new Server(801);
            TestServer.Start();
            Client TestClient = new Client(802);
            var WaitTask = TestClient.StartAsync("127.0.0.1", 801);
            byte[] BytesToSend = Encoding.Unicode.GetBytes("ASD");
            WaitTask.Wait();
            Task<Message> TaskToWait = TestClient.SendMessageAsync(BytesToSend);
            TestClient.ServerMessages[Message.LastMessageID]
                .Add(new Chunk(Chunk.MessageTypes.Text, Message.LastMessageID, 1, Encoding.Unicode.GetBytes("TTT"),
                    true));
            Assert.That(TaskToWait.Wait(1000), Is.True);
            Assert.That(Encoding.Unicode.GetString(TaskToWait.Result.ContainedMessage), Is.EqualTo("TTT"));
            TestClient.Stop();
            TestServer.Stop();
        }
        public delegate void TestDelegate();
        [Test]
        public void RecieveData_ResultIsNotBuffer_Exception()
        {
            TestDelegate Delegate = new TestDelegate(() => { });
            Assert.That(() => new Client().ReceiveData(Delegate.BeginInvoke(null, "A")), Throws.Exception);
        }
    }
}
