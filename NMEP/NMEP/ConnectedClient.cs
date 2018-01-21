using System;
using System.Collections.Generic;
using System.Net.Sockets;
namespace NMEP
{
    /// <summary>
    /// Представляет клиента, подключенного к серверу
    /// </summary>
    public class ConnectedClient
    {
        //Порядковый номер клиента
        public int ClientID { get; internal set; }
        //Количеттво байтов в одном пакете
        public ushort BytesPerChunk { get; internal set; }
        //Буффер с последним полученным от клиента сообщением
        internal byte[] Buffer;
        //Словарь частично полученных от клиента сообщений
        internal Dictionary<ushort, Message> ClientMessages;

        public delegate void MessageEventHandler(ConnectedClient iClient, Message iMessage);
        /// <summary>
        /// Событие возникает когда от клиента получено законченное сообщение
        /// </summary>
        public event MessageEventHandler MessageReceived;

        /// <summary>
        /// Создает экземпляр, инициализируя его указанными параметрами
        /// </summary>
        /// <param name="iClientID">Уникальный ID клиента</param>
        /// <param name="iBytesPerChunk">Количество байтов в одном сообщение</param>
        /// <param name="iSocket">Связанный с клиентом сокет</param>
        public ConnectedClient(int iClientID, ushort iBytesPerChunk, Socket iSocket)
        {
            ClientID = iClientID;
            BytesPerChunk = iBytesPerChunk;
            Buffer = new byte[iBytesPerChunk];
            ClientMessages = new Dictionary<ushort, Message>();
            //Получение сообщения с указанного сокета
            iSocket.BeginReceive(Buffer, 0, iBytesPerChunk, SocketFlags.None, ReceiveData, iSocket);
        }
        internal void ReceiveData(IAsyncResult iResult)
        {
            //AsyncResult должен быть сокетом, получаем его
            Socket ClientSocket = iResult.AsyncState as Socket;
            if (ClientSocket == null)
                throw new ArgumentException("Result is not socket");
            //Получаем количество принятых байтов
            int BytesReceived;
            try
            {
                BytesReceived = ClientSocket.EndReceive(iResult);
            }
            catch(ObjectDisposedException)
            {
                return;
            }
            if (BytesReceived == 0)
                return;
            //MinifiedBuffer - буффер, подогнанный под размер полученного собщения
            byte[] MinifiedBuffer;
            if (BytesReceived < Buffer.Length)
            {
                MinifiedBuffer = new byte[BytesReceived];
                Array.Copy(Buffer, MinifiedBuffer, BytesReceived);
            }
            else
                MinifiedBuffer = Buffer;

            //Иниуиализируем полученным сообщением чанк
            Chunk NewChunk = new Chunk(MinifiedBuffer);
            Message MessageToWrite;
            //Пытаемся найти сообщение с полученным ID
            if (!ClientMessages.TryGetValue(NewChunk.MessageID, out MessageToWrite))
            {
                //Если его еще нет, добавляем в словарь
                MessageToWrite = new Message(NewChunk.MessageID);
                ClientMessages.Add(NewChunk.MessageID, MessageToWrite);
            }
            //Добавляем чанк в сообщение
            MessageToWrite.Add(NewChunk);
            //Если сообщение закончено
            if (MessageToWrite.IsCompleted)
            {
                //Генерируем соответствующее событие
                if (MessageReceived != null) 
                    MessageReceived(this, MessageToWrite);
                //Убираем сообщение из словаря
                ClientMessages.Remove(MessageToWrite.MessageID);
            }
            //Начинаем принимать следующий пакет
            ClientSocket.BeginReceive(Buffer, 0, Consts.BYTES_PER_CHUNK, SocketFlags.None,
                ReceiveData, ClientSocket);
        }
    }
}
