using System.Linq;
using NMEP;
using NUnit.Framework;
namespace NMEPTests
{
    [TestFixture]
    class MessageTests
    {
        [Test]
        public void NewMessageWithFewChunks_AllAsExpected()
        {
            byte[] MessageBytes = {1, 2, 3, 4, 5};
            Message TestMessage = new Message(MessageBytes, Chunk.MessageTypes.Text, 8);
            Assert.That(TestMessage.Check(), Is.True);
            Assert.That(TestMessage.ContainedMessage.SequenceEqual(MessageBytes), Is.True);

            Assert.That(TestMessage.Chunks.Count, Is.EqualTo(3));
            byte[] FirstChunkBytes = {1, 2};
            Assert.That(TestMessage.Chunks[0].Type, Is.EqualTo(Chunk.MessageTypes.Text));
            Assert.That(TestMessage.Chunks[0].IsLastChunk, Is.False);
            Assert.That(TestMessage.Chunks[0].Data.SequenceEqual(FirstChunkBytes));
            byte[] SecondChunkBytes = { 3, 4 };
            Assert.That(TestMessage.Chunks[1].Type, Is.EqualTo(Chunk.MessageTypes.Internal));
            Assert.That(TestMessage.Chunks[1].IsLastChunk, Is.False);
            Assert.That(TestMessage.Chunks[1].Data.SequenceEqual(SecondChunkBytes));
            byte[] ThirdChunkBytes = { 5 };
            Assert.That(TestMessage.Chunks[2].Type, Is.EqualTo(Chunk.MessageTypes.Internal));
            Assert.That(TestMessage.Chunks[2].IsLastChunk, Is.True);
            Assert.That(TestMessage.Chunks[2].Data.SequenceEqual(ThirdChunkBytes));
        }
        [Test]
        public void NewMessageWithNonType_Exception()
        {
            Assert.That(() => new Message(new byte[1], Chunk.MessageTypes.None, Consts.BYTES_PER_CHUNK), Throws.Exception);
        }
        [Test]
        public void NewFileMessage()
        {
            Message TestMessage = new Message(new byte[1], Chunk.MessageTypes.File, Consts.BYTES_PER_CHUNK);
            Assert.That(TestMessage.Chunks[0].Type, Is.EqualTo(Chunk.MessageTypes.File));
        }
        [Test]
        public void NewMessageFromChunks_InconsistentChunkNumbers_CantCheck()
        {
            Message TestMessage = new Message(123);
            TestMessage.Add(new Chunk(Chunk.MessageTypes.File, 123, 1, new byte[1], false));
            TestMessage.Add(new Chunk(Chunk.MessageTypes.Internal, 123, 3, new byte[1], true));
            Assert.That(TestMessage.Check(), Is.False);
        }
        [Test]
        public void NewMessageFromChunks_FirstChunkInternal_CantCheck()
        {
            Message TestMessage = new Message(123);
            TestMessage.Add(new Chunk(Chunk.MessageTypes.File, 123, 1, new byte[1], false));
            TestMessage.Add(new Chunk(Chunk.MessageTypes.File, 123, 2, new byte[1], true));
            Assert.That(TestMessage.Check(), Is.False);
        }
        [Test]
        public void NewMessageFromChunks_FirstChunkWithFalseType_CantCheck()
        {
            Message TestMessage = new Message(123);
            TestMessage.Add(new Chunk(Chunk.MessageTypes.Internal, 123, 1, new byte[1], false));
            TestMessage.Add(new Chunk(Chunk.MessageTypes.Internal, 123, 2, new byte[1], true));
            Assert.That(TestMessage.Check(), Is.False);
        }
        [Test]
        public void AddChunk_InvalidID_False()
        {
            Message TestMessage = new Message(123);
            Assert.That(TestMessage.Add(new Chunk(Chunk.MessageTypes.Text, 321, 1, new byte[1], true)), Is.False);
        }
        [Test]
        public void CantCheck_NullContainedMessage()
        {
            Message TestMessage = new Message(123);
            TestMessage.Add(new Chunk(Chunk.MessageTypes.Internal, 123, 1, new byte[1], false));
            TestMessage.Add(new Chunk(Chunk.MessageTypes.Internal, 123, 2, new byte[1], true));
            Assert.That(TestMessage.ContainedMessage, Is.Null);
        }
        [Test]
        public void NewMessage_TooFewBytesPerChunk_Exception()
        {
            Assert.That(() => new Message(new byte[1], Chunk.MessageTypes.Text, 1), Throws.Exception);
        }
    }
}
