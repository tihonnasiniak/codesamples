using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NMEP
{
    /// <summary>
    /// Клиент, подключающийся к NMEP серверу
    /// </summary>
    public class Client
    {
        /// <summary>
        /// Сокет, связанный с клиентом
        /// </summary>
        internal Socket ClientSocket { get; set; }
        /// <summary>
        /// Соловарь сообщений, полученных от сервера, связанных с их ID
        /// </summary>
        internal Dictionary<ushort, Message> ServerMessages { get; set; }
        /// <summary>
        /// Создает экземпляр клиента, работающего на указанном порту
        /// </summary>
        /// <param name="iPort">Порт, связанный с клиентом</param>
        public Client(int iPort = Consts.NMEP_PORT + 1)
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ServerMessages = new Dictionary<ushort, Message>();
            ClientSocket.Bind(new IPEndPoint(IPAddress.Any, iPort));
        }
        /// <summary>
        /// Подключает клиент к серверу
        /// </summary>
        /// <param name="iServerIP">IP адрес сервера</param>
        /// <param name="iServerPort">Порт, прослушиваемый сервером</param>
        public void Start(string iServerIP, int iServerPort = Consts.NMEP_PORT)
        {
            //Вызываем асинхронную функцию и ждем ее завершения
            StartAsync(iServerIP, iServerPort).Wait();
        }
        /// <summary>
        /// Останавливает клиент и закрывает подключение
        /// </summary>
        public void Stop()
        {
            ClientSocket.Disconnect(false);
            ClientSocket.Close(1);
        }
        /// <summary>
        /// Асинхронно подключает клиент к серверу
        /// </summary>
        /// <param name="iServerIP">IP адрес сервера</param>
        /// <param name="iServerPort">Порт, прослушиваемый сервером</param>
        /// <returns></returns>
        public async Task StartAsync(string iServerIP, int iServerPort = Consts.NMEP_PORT)
        {
            //Инициализируем подключение к серверу по заданному адресу и порту
            await Task.Factory.FromAsync(
                ClientSocket.BeginConnect, ClientSocket.EndConnect,
                new IPEndPoint(IPAddress.Parse(iServerIP), iServerPort), null);
            //Создаем буфер для handsahke
            List<byte> HandshakeBytes = new List<byte>();
            //Handshake имеет следующий формат:
            //HANDSHAKE_SIGN (byte), PROTOCOL_VERSION (byte), BYTES_PER_CHUNK (ushort)
            HandshakeBytes.Add(Consts.HANDSHAKE_SIGN);
            HandshakeBytes.Add(Consts.PROTOCOL_VERSION);
            HandshakeBytes.AddRange(BitConverter.GetBytes(Consts.BYTES_PER_CHUNK));
            byte[] HandshakeArray = HandshakeBytes.ToArray();
            //Посылаем handshake серверу
            await Task<int>.Factory.FromAsync(
                ClientSocket.BeginSend(HandshakeArray, 0, HandshakeArray.Length, SocketFlags.None, null, ClientSocket),
                ClientSocket.EndSend);
            
            byte[] Buffer = new byte[Consts.BYTES_PER_CHUNK];
            //Начинаем получение сообщения от сервера
            ClientSocket.BeginReceive(Buffer, 0, Consts.BYTES_PER_CHUNK, SocketFlags.None, 
                ReceiveData, Buffer);
        }
        /// <summary>
        /// Получает пакет данных от сервера
        /// </summary>
        /// <param name="iResult">Буфер для полученных данных</param>
        internal void ReceiveData(IAsyncResult iResult)
        {
            //AsyncResult должен быть массивом байтов
            byte[] Buffer = iResult.AsyncState as byte[];
            if(Buffer == null)
                throw new ArgumentException("Result is not byte array");
            //Получаем количество полученных байтов
            int BytesReceived;
            try
            {
                BytesReceived = ClientSocket.EndReceive(iResult);
            }
            catch(ObjectDisposedException)
            {
                return;
            }
            //MinifiedBuffer - буфер, подогнанный под размер полученного сообщения
            byte[] MinifiedBuffer;
            if (BytesReceived < Buffer.Length)
            {
                MinifiedBuffer = new byte[BytesReceived];
                Array.Copy(Buffer, MinifiedBuffer, BytesReceived);
            }
            else
                MinifiedBuffer = Buffer;
            //Создаем чанк из полученных данных
            Chunk NewChunk = new Chunk(MinifiedBuffer);
            Message MessageToWrite;
            //Пробуем искать сообщение по ID
            if (ServerMessages.TryGetValue(NewChunk.MessageID, out MessageToWrite))
                //Добавляем к нему полученный чанк
                MessageToWrite.Add(NewChunk);
            //Начинаем получение следующего сообщения
            ClientSocket.BeginReceive(Buffer, 0, Consts.BYTES_PER_CHUNK, SocketFlags.None,
                ReceiveData, Buffer);

        }
        /// <summary>
        /// Отправляет на сервер сообщение и возвращает ответ
        /// </summary>
        /// <param name="iMessage">Сообщение для сервера</param>
        /// <returns>Ответ сервера на сообщение</returns>
        public async Task<Message> SendMessageAsync(byte[] iMessage)
        {
            //Создаем сообщение из переданных данных
            Message MessageToSend = new Message(iMessage, Chunk.MessageTypes.Text, Consts.BYTES_PER_CHUNK);
            //Создаем сообщение для получения ответа от сервера
            Message ServerResponse = new Message(MessageToSend.MessageID);
            //Добавляем его в словарь
            ServerMessages.Add(MessageToSend.MessageID, ServerResponse);

            //Последовательно отправляем чанки серверу
            foreach (Chunk C in MessageToSend.Chunks)
            {
                int BytesSent = await Task<int>.Factory.FromAsync(
                    ClientSocket.BeginSend(C.ChunkBytes, 0, C.ChunkBytes.Length, SocketFlags.None, null, ClientSocket),
                    ClientSocket.EndSend);
            }
            //Возвращаем задачу, которая завершится когда будет получено законченный ответ
            return await ServerResponse.IsCompletedTask;
        }
    }
}
