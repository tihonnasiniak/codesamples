using System.Linq;
using System.Text;
using NUnit.Framework;
using NMEP;
namespace NMEPTests
{
    [TestFixture]
    class ChunkTests
    {
        [Test]
        public void MinLength_EqualToSumOfServiceBytes()
        {
            Assert.That(Chunk.MIN_LENGTH, Is.EqualTo(6));
        }
        [Test]
        public void ChunkBytes_BitwiseEqualToExpected()
        {
            byte[] Data = {0xFF};
            Chunk TestChunk = new Chunk(Chunk.MessageTypes.Text, 1001, 1000, Data, true);
            Assert.That(TestChunk.Check(), Is.True);
            byte[] Returned = TestChunk.ChunkBytes;
            //E2 - TXT start
            //E9 03 - 1001
            //E8 03 - 1000
            //FF - data
            //F2 - last chunk end
            byte[] Expected = {0xE2, 0xE9, 0x03, 0xE8, 0x03, 0xFF, 0xF2};
            Assert.That(Returned.Length, Is.EqualTo(Expected.Length));
            for (int i = 0; i < Returned.Length; ++i)
                Assert.That(Returned[i], Is.EqualTo(Expected[i]));
        }
        [Test]
        public void ChunkFromBytes_AllAsExpected()
        {
            //E2 - TXT start
            //E9 03 - 1001
            //E8 03 - 1000
            //FF - data
            //F2 - last chunk end
            byte[] RawChunk = { 0xE2, 0xE9, 0x03, 0xE8, 0x03, 0xFF, 0xF2 };
            Chunk TestChunk = new Chunk(RawChunk);
            Assert.That(TestChunk.Check(), Is.True);

            Assert.That(TestChunk.Type, Is.EqualTo(Chunk.MessageTypes.Text));
            Assert.That(TestChunk.MessageID, Is.EqualTo(1001));
            Assert.That(TestChunk.ChunkNumber, Is.EqualTo(1000));
            byte[] ExpectedData = {0xFF};
            Assert.That(TestChunk.Data.SequenceEqual(ExpectedData));
            Assert.That(TestChunk.IsLastChunk, Is.True);
        }
        [Test]
        public void ChunkFromBytes_TooFewBytes_Exception()
        {
            //FF - Incorrect start sign
            //E9 03 - 1001
            //E8 03 - 1000
            //FF - data
            //F2 - last chunk end
            byte[] RawChunk = { 0xFF, 0xE9 };
            Assert.That(() => new Chunk(RawChunk), Throws.Exception);
        }
        [Test]
        public void ChunkFromBytes_IncorrectStartSign_Exception()
        {
            //FF - Incorrect start sign
            //E9 03 - 1001
            //E8 03 - 1000
            //FF - data
            //F2 - last chunk end
            byte[] RawChunk = { 0xFF, 0xE9, 0x03, 0xE8, 0x03, 0xFF, 0xF2 };
            Assert.That(() => new Chunk(RawChunk), Throws.Exception);
        }
        [Test]
        public void ChunkFromBytes_IncorrectEndSign_Exception()
        {
            //E2 - TXT start
            //E9 03 - 1001
            //E8 03 - 1000
            //FF - data
            //00 - incorrect end sign
            byte[] RawChunk = { 0xE2, 0xE9, 0x03, 0xE8, 0x03, 0xFF, 0x00 };
            Assert.That(() => new Chunk(RawChunk), Throws.Exception);
        }
        [Test]
        public void NewChunk_NullMessageID_CantCheck()
        {
            Chunk TestChunk = new Chunk(Chunk.MessageTypes.XML, 0, 1, Encoding.Unicode.GetBytes("A"), true);
            Assert.That(TestChunk.Check(), Is.False);
            Assert.That(TestChunk.Type, Is.EqualTo(Chunk.MessageTypes.XML));
        }
    }
}
