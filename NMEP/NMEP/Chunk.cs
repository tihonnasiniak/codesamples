using System;
using System.Collections.Generic;

namespace NMEP
{
    public class Chunk
    {
        /// <summary>
        /// Виды сообщений
        /// </summary>
        public enum MessageTypes
        {
            Text,
            XML,
            File,
            /// <summary>
            /// Внутренний чанк, общий для всех типов сообщений
            /// </summary>
            Internal,
            /// <summary>
            /// Ошибка
            /// </summary>
            None
        }
        //Минимальная длина чанка - признак начала, MessageID, номер чанка, признак конца
        public const int MIN_LENGTH = (sizeof(byte) * 2) + (sizeof(ushort) * 2);
        //Признаки начала сообщения разных типов
        internal const byte MESSAGE_START_SIGN = 0xE1;
        internal const byte TEXT_START_SIGN = 0xE2;
        internal const byte XML_START_SIGN = 0xE3;
        internal const byte FILE_START_SIGN = 0xE4;
        //Признак конца чанка
        internal const byte MESSAGE_END_SIGN = 0xF1;
        //Признак конца последнего чанка в сообщении
        internal const byte LAST_CHUNK_SIGN = 0xF2;

        internal byte FirstByte { get; set; }
        internal byte LastByte { get; set; }
        /// <summary>
        /// Уникальный ID сообщения
        /// </summary>
        public ushort MessageID { get; internal set; }
        /// <summary>
        /// Номер чанка внутри сообщения
        /// </summary>
        public ushort ChunkNumber { get; internal set; }
        /// <summary>
        /// Полезная нагрузка чанка
        /// </summary>
        public byte[] Data { get; internal set; }
        /// <summary>
        /// Байтовое представление чанка
        /// </summary>
        public byte[] ChunkBytes
        {
            get
            {
                List<byte> Bytes = new List<byte>();
                Bytes.Add(FirstByte);
                Bytes.AddRange(BitConverter.GetBytes(MessageID));
                Bytes.AddRange(BitConverter.GetBytes(ChunkNumber));
                Bytes.AddRange(Data);
                Bytes.Add(LastByte);
                return Bytes.ToArray();
            }
        }

        /// <summary>
        /// Получает тип чанка
        /// </summary>
        public MessageTypes Type
        {
            get
            {
                switch (FirstByte)
                {
                    case (MESSAGE_START_SIGN):
                        return MessageTypes.Internal;
                    case (TEXT_START_SIGN):
                        return MessageTypes.Text;
                    case (XML_START_SIGN):
                        return MessageTypes.XML;
                    case (FILE_START_SIGN):
                        return MessageTypes.File;
                    default:
                        return MessageTypes.None;
                }
            }
        }
        /// <summary>
        /// Определяет, является ли чанк последним в сообщении
        /// </summary>
        public bool IsLastChunk { get { return LastByte == LAST_CHUNK_SIGN; } }
        /// <summary>
        /// Проверяет соответствие чанка стандарту
        /// </summary>
        /// <returns></returns>
        public bool Check()
        {
            //Если не удается распознать тип по первому байту
            if (Type == MessageTypes.None)
                return false;
            //Если последний тбайт не равен ожидаемым
            if (LastByte != MESSAGE_END_SIGN && LastByte != LAST_CHUNK_SIGN)
                return false;
            //MessageID и номер чанка не могут быть нулевыми
            if (MessageID == 0 || ChunkNumber == 0)
                return false;
            return true;
        }
        /// <summary>
        /// Создает новый чанк из "сырых" данных
        /// </summary>
        /// <param name="iData">"Сырые" данные со всопмогательной информацией</param>
        public Chunk(byte[] iData)
        {
            //Если данных меньше, чем минимальная длина чанка
            if (iData.Length < MIN_LENGTH)
                throw new ArgumentException("Data is too short");
            FirstByte = iData[0];
            LastByte = iData[iData.Length - 1];
            MessageID = BitConverter.ToUInt16(iData, sizeof(byte));
            ChunkNumber = BitConverter.ToUInt16(iData, sizeof(byte) + sizeof(ushort));
            //Длина полезной нагрузки
            int BytesToCopy = iData.Length - MIN_LENGTH;
            Data = new byte[BytesToCopy];
            Array.Copy(iData, sizeof(byte) + sizeof(ushort) * 2, Data, 0, BytesToCopy);
            //Если чанк не проходит проверку
            if (!Check())
                throw new ArgumentException("Unexpected first of last byte");
        }
        /// <summary>
        /// Создает новый чанк с заданной полезной нагрузкой
        /// </summary>
        /// <param name="iType">Тип чанка</param>
        /// <param name="iMessageID">Уникальный ID сообщения</param>
        /// <param name="iChunkNumber">Номер чанка внутри сообщения</param>
        /// <param name="iData">Полезная нагрузка</param>
        /// <param name="iIsLastChunk">True если этот чанк - последний в сообщении</param>
        public Chunk(MessageTypes iType, ushort iMessageID, ushort iChunkNumber, byte[] iData, bool iIsLastChunk)
        {
            //Устанавливаем первый бат в зависимости от типа
            switch(iType)
            {
                case (MessageTypes.Text):
                    FirstByte = TEXT_START_SIGN;
                    break;
                case (MessageTypes.XML):
                    FirstByte = XML_START_SIGN;
                    break;
                case (MessageTypes.File):
                    FirstByte = FILE_START_SIGN;
                    break;
                case (MessageTypes.Internal):
                    FirstByte = MESSAGE_START_SIGN;
                    break;
                default:
                    throw new ArgumentException("Invalid Message Type");
            }
            //Устанавливаем последний байт
            LastByte = iIsLastChunk ? LAST_CHUNK_SIGN : MESSAGE_END_SIGN;
            MessageID = iMessageID;
            ChunkNumber = iChunkNumber;
            Data = iData;
        }
    }
}