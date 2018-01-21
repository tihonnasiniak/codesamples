using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NMEP
{
    public class Message
    {
        //Минимальный размер пакета - размер чанка с одм байтом полезной нагрузки
        public const int MIN_BYTES_PER_CHUNK = Chunk.MIN_LENGTH + 1;
        //ID последнего сообщения для удобства генерации MessageID
        public static ushort LastMessageID = 0;

        internal TaskCompletionSource<Message> IsCompletedSource;
        /// <summary>
        /// Выполняется когда сообщение завершено
        /// </summary>
        public Task<Message> IsCompletedTask { get { return IsCompletedSource.Task; } }
        /// <summary>
        /// True если сообщение завершено, false в противном случае
        /// </summary>
        public bool IsCompleted { get { return IsCompletedTask.IsCompleted; } }
        /// <summary>
        /// Уникальный номер сообщения
        /// </summary>
        public ushort MessageID { get; internal set; }
        /// <summary>
        /// Спосок чанков сообщения
        /// </summary>
        public List<Chunk> Chunks { get; internal set; }
        /// <summary>
        /// Полезная нагрузка, содержащаяся в сообщении
        /// </summary>
        public byte[] ContainedMessage
        {
            get
            {
                //Если сообщение не прошло проверку, то возращается null
                if (!Check())
                    return null;
                //Сбор полезной нагрузки со всех чанков
                List<byte> Bytes = new List<byte>();
                foreach (Chunk C in Chunks)
                    Bytes.AddRange(C.Data);
                return Bytes.ToArray();
            }
        }
        /// <summary>
        /// Проверка сообщения на законченность
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            //Сортировка чанков по внутреннему номеру
            Chunks.Sort((iC1, iC2) => iC1.ChunkNumber.CompareTo(iC2.ChunkNumber));
            for(int i=0; i<Chunks.Count; ++i)
            {
                //Если номера чанков иду не поп порядку
                if (Chunks[i].ChunkNumber != (i + 1))
                    return false;
                //Если тип внутреннего чанка не internal
                if (i != 0 && Chunks[i].Type != Chunk.MessageTypes.Internal)
                    return false;
            }
            //Если первый чанк не соответствует ожидаемому типу
            if (Chunks[0].Type != Chunk.MessageTypes.Text && Chunks[0].Type != Chunk.MessageTypes.XML &&
                Chunks[0].Type != Chunk.MessageTypes.File)
                return false;
            //Если последний чанк - промежуточный
            if (!Chunks[Chunks.Count - 1].IsLastChunk)
                return false;
            return true;
        }

        /// <summary>
        /// Создает новое сообщение с заданной полезной нагрузкой
        /// </summary>
        /// <param name="iBytes">Полезная нагрузка в виде байтов</param>
        /// <param name="iMessageType">Тип сообщения</param>
        /// <param name="iBytesPerChunk">Количество байтов в чанке</param>
        /// <param name="iMessageID">Уникальный ID сообщения</param>
        public Message(byte[] iBytes, Chunk.MessageTypes iMessageType, int iBytesPerChunk, ushort iMessageID = 0)
        {
            //Если количество байтов в чанке меньше минимального
            if (iBytesPerChunk < MIN_BYTES_PER_CHUNK)
                throw new ArgumentException("Too few bytes per chunk");
            //Если MessageID равняется нулю, генерируется новый MessageID
            if (iMessageID == 0)
                MessageID = ++LastMessageID;
            else
                MessageID = iMessageID;
            Chunks = new List<Chunk>();
            IsCompletedSource = new TaskCompletionSource<Message>();
            //DataPerChunk - длина полезной нагрузки в чанке
            int DataPerChunk = iBytesPerChunk - Chunk.MIN_LENGTH;
            //Смещение - кол-во записанных байтов
            int Offset = 0;
            ushort ChunkNumber = 1;
            //В цикле полезная нагрузка разбивается по чанкам
            //Цикл пока записано меньше длины полезной нагрузки
            while(Offset < iBytes.Length)
            {
                //Определяем, будет ли этот чанк последним
                bool IsLastChunk = Offset + DataPerChunk >= iBytes.Length;
                //Длина полезной нагрузки в этом чанке
                int BytesToCopy = IsLastChunk ? iBytes.Length - Offset : DataPerChunk;
                byte[] ChunkBytes = new byte[BytesToCopy];
                //Копируем данные в буфер
                Array.Copy(iBytes, Offset, ChunkBytes, 0, BytesToCopy);

                //Создаем чанк с данными из буфера
                Add(new Chunk(Offset == 0 ? iMessageType : Chunk.MessageTypes.Internal, MessageID, ChunkNumber, 
                    ChunkBytes, Offset + DataPerChunk >= iBytes.Length));

                Offset += BytesToCopy;
                ++ChunkNumber;
            }
        }
        /// <summary>
        /// Создает пустое сообщение с заданным ID
        /// </summary>
        /// <param name="iMessageID">Уникальный ID сообщения</param>
        public Message(ushort iMessageID)
        {
            MessageID = iMessageID;
            Chunks = new List<Chunk>();
            IsCompletedSource = new TaskCompletionSource<Message>();
        }
        /// <summary>
        /// Добавляет в сообщение чанк
        /// </summary>
        /// <param name="iChunk">Чанк для добавления</param>
        /// <returns>True если добавление прошло успешно, false в противном случае</returns>
        public bool Add(Chunk iChunk)
        {
            //Если MessageID в чанке не соответствует ID сообщения
            if (iChunk.MessageID != MessageID)
                return false;
            Chunks.Add(iChunk);
            //Если проверка прошла успешно, то сообщение завершено
            if (Check())
                IsCompletedSource.SetResult(this);
            return true;
        }
    }
}
