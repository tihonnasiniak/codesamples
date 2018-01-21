using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("NMEPTests")]
namespace NMEP
{
    /// <summary>
    /// Сервер протокола NMEP
    /// </summary>
    public class Server
    {
        //Сокет для входящих подключений
        internal Socket ServerSocket;
        //Словарь клиентов, связанных с их сокетом
        public Dictionary<Socket, ConnectedClient> Clients;
        /// <summary>
        /// Создает экземпляр сервера, принимающего подключения на указанном порте
        /// </summary>
        /// <param name="iPort">Порт, прослушиваемый сервером</param>
        public Server(int iPort = Consts.NMEP_PORT)
        {
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Clients = new Dictionary<Socket, ConnectedClient>();
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, iPort));
        }
        /// <summary>
        /// После выполнения этой функции сервер начинает принимать вхоодящие подключения
        /// </summary>
        public void Start()
        {
            ServerSocket.Listen(0);
            ServerSocket.BeginAccept(AcceptConnection, ServerSocket);
        }
        /// <summary>
        /// Останавливает сервер и закрывает все подклчения
        /// </summary>
        public void Stop()
        {
            foreach (KeyValuePair<Socket, ConnectedClient> P in Clients)
            {
                P.Key.Disconnect(false);
                P.Key.Close();
            }
            ServerSocket.Close();
        }

        public delegate void ConnectionEventHandler(ConnectedClient iClient);
        /// <summary>
        /// Событие возникает когда к серверу подключается клиент
        /// </summary>
        public event ConnectionEventHandler ClientConnected;

        public delegate void MessageEventHandler(ConnectedClient iClient, Message iMessage);
        /// <summary>
        /// Событие возникает когда сервер получает законченное сообщение
        /// </summary>
        public event MessageEventHandler MessageReceived;

        /// <summary>
        /// Посылает указанному клиенту сообщение
        /// </summary>
        /// <param name="iMessage">Последовательность байтов, отправялемая клиенту</param>
        /// <param name="iClient">Клиент, которому будет отправлено сообщение</param>
        /// <param name="iMessageID">MessageID отправляемого сообщения (дожно соответствовать MessageID принятого)</param>
        /// <returns></returns>
        public async Task SendMessageAsync(byte[] iMessage, ConnectedClient iClient, ushort iMessageID)
        {
            //Создается новое текстовое сообщение из полученных байтов
            Message MessageToSend = new Message(iMessage, Chunk.MessageTypes.Text, iClient.BytesPerChunk, iMessageID);
            //Получается сокет клиента
            Socket ClientSocket = Clients.FirstOrDefault(iX => iX.Value.ClientID == iClient.ClientID).Key;
            foreach (Chunk C in MessageToSend.Chunks)
            {
                //Каждая часть сообщения последовательно отправляется клиенту
                int BytesSent = await Task<int>.Factory.FromAsync(
                    ClientSocket.BeginSend(C.ChunkBytes, 0, C.ChunkBytes.Length, SocketFlags.None, null, ClientSocket),
                    ClientSocket.EndSend);
            }
        }
        /// <summary>
        /// Принимает входящее подключение
        /// </summary>
        internal void AcceptConnection(IAsyncResult iResult)
        {
            //Получаем сокет подключения
            Socket NewSocket;
            try
            {
                NewSocket = ServerSocket.EndAccept(iResult);
            }
            catch(ObjectDisposedException)
            {
                return;
            }
            //Буффер для handshake'а
            byte[] Buffer = new byte[Consts.HANDSHAKE_LENGTH];

            //Получение и обработка hanshake'а
            NewSocket.Receive(Buffer);
            if (Buffer[0] != Consts.HANDSHAKE_SIGN || Buffer[1] != Consts.PROTOCOL_VERSION)
            {
                NewSocket.Disconnect(false);
                NewSocket.Close();
            }
            else
            {
                //Получаем от клиента размер пакета
                ushort BytesPerChunkRecieved = BitConverter.ToUInt16(Buffer, sizeof(byte) * 2);

                //Создаем клиента
                ConnectedClient NewClient = new ConnectedClient(Clients.Count + 1, BytesPerChunkRecieved, NewSocket);
                //Если клиет генерирует событие полученного сообщения, сервер генерирует событие
                NewClient.MessageReceived += (iClient, iMessage) =>
                {
                    if (MessageReceived != null) MessageReceived(iClient, iMessage);
                };
                //Клиент добавляется в словарь
                Clients.Add(NewSocket, NewClient);
                //Генерируется событие
                if (ClientConnected != null)
                    ClientConnected(NewClient);
            }
            //Принимаем следующее подключение
            ServerSocket.BeginAccept(AcceptConnection, ServerSocket);
        }
    }
}
