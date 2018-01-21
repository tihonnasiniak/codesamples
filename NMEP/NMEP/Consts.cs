namespace NMEP
{
    public class Consts
    {
        //Константы, используемые библиотекой

        //Handshake, посылаемый клиентом серверу при подключении
        public const byte HANDSHAKE_SIGN = 0xEA;
        //Ответ сервера на handshake
        public const byte RESPOND_SIGN = 0xEB;

        //Константа номера первой версии протокола
        public const byte PROTOCOL_VERSION = 0x01;
        //Количество байтов в одном сообщении по умолчанию
        public const ushort BYTES_PER_CHUNK = 1024;
        //Длина hanshake'а
        public const int HANDSHAKE_LENGTH = sizeof(byte) * 2 + sizeof(ushort);

        //Порт по умолчанию, который прослшивает сервер
        public const int NMEP_PORT = 777;
    }
}
