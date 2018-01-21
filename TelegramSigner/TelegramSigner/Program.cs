using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;

namespace TelegramSigner
{
    class Program
    {
        /// <summary>
        /// Имя файла сохранения
        /// </summary>
        internal const string SAVE_FILE_NAME = "params.xml";
        //Имена XML аттрибутов и элементов в файле сохранения
        internal const string XML_ADMIN = "admin";
        internal const string XML_TOKEN = "token";
        internal const string XML_CHAT = "chat";

        /// <summary>
        /// Экземпляр клиента для взаимодействия с API Telegram
        /// </summary>
        internal static TelegramBotClient Bot;
        /// <summary>
        /// ID чата с админом
        /// </summary>
        internal static long Admin = 0;
        /// <summary>
        /// Список ID чатов, которым дан доступ к боту
        /// </summary>
        internal static List<long> Chats = new List<long>();
        /// <summary>
        /// Возвращает список всех чатов получивших доступ
        /// </summary>
        internal static string ChatsListString
        {
            get
            {
                StringBuilder ChatsList = new StringBuilder();
                foreach (long ChatID in Chats)
                {
                    Task<Chat> ChatTask = Bot.GetChatAsync(ChatID);
                    ChatTask.Wait();
                    Chat Chat = ChatTask.Result;
                    ChatsList.AppendFormat("{0} {1} {2} {3}\n", ChatID, Chat.Username, Chat.FirstName, Chat.LastName);
                }
                return ChatsList.ToString();
            }
        }
        /// <summary>
        /// Криптопровайдер для подписи документов
        /// </summary>
        internal static RSACryptoServiceProvider CSP;

        static void Main(string[] args)
        {
            //Загружаем данные из файла
            Load();
            //Инициализируем параметры ключа
            CspParameters CSPParams = new CspParameters();
            //Тип ключа - для подписи
            CSPParams.KeyNumber = (int)KeyNumber.Signature;
            CSPParams.KeyContainerName = "TelegramSignerKey";
            //Создаем экземпляр криптопровайдера
            CSP = new RSACryptoServiceProvider(2048, CSPParams);

            //Привязываем к событию функцию, обрабатывающую полученное сообщение
            Bot.OnMessage += MessageRecieved;
            //Запускаем бота
            Bot.StartReceiving();
            string Command = "";
            while (Command != "q")
            {               
                Command = Console.ReadLine();
                if (Command == "list")
                    Console.WriteLine(ChatsListString);
                else
                {
                    long Parsed;
                    //Если введенная команда - число, даем этому ID админский доступ
                    if (long.TryParse(Command, out Parsed))
                    {
                        try
                        {
                            //Получаем чат по ID и даем ему доступ
                            Task<Chat> ChatTask = Bot.GetChatAsync(Parsed);
                            ChatTask.Wait();
                            GiveAccess(ChatTask.Result);
                            //Устанавливаем ID в качестве админа
                            Admin = Parsed;

                            Console.WriteLine($"{Parsed} получил доступ администратора");
                            Bot.SendTextMessageAsync(Parsed, "Вам предоставлен доступ администратора");

                            //Сохраняем изменения в файл
                            XDocument Doc = XDocument.Load(SAVE_FILE_NAME);
                            Doc.Root.Attribute(XML_ADMIN).Value = Parsed.ToString();
                            Doc.Save(SAVE_FILE_NAME);
                        }
                        catch { }
                    }
                }
            }
        }
        /// <summary>
        /// Обрабатывает входящее сообщение
        /// </summary>
        internal static async void MessageRecieved(object sender, MessageEventArgs iArgs)
        {
            //Получаем сообщение
            Message RecievedMessage = iArgs.Message;
            //Если нет админа, сообщаем об этом в консоль
            if (Admin == 0)
            {
                Console.WriteLine($"{RecievedMessage.Chat.Id} {RecievedMessage.Chat.Username} запросил доступ");
                await Bot.SendTextMessageAsync(RecievedMessage.Chat.Id, "Запрос доступа отправлен");
                return;
            }
            //Если админ написал текстовое сообщение
            else if (RecievedMessage.Chat.Id == Admin && RecievedMessage.Type == MessageType.TextMessage)
            {
                //Команда на предоставление доступа указанному ID
                if (RecievedMessage.Text.StartsWith("/access"))
                {
                    long AccessID;
                    if (long.TryParse(RecievedMessage.Text.Substring(7), out AccessID))
                    {
                        try
                        {
                            GiveAccess(await Bot.GetChatAsync(AccessID));
                        }
                        catch (ChatNotFoundException)
                        { }
                    }
                }
                //Команда на отзыв доступа у указанного ID
                if (RecievedMessage.Text.StartsWith("/kick"))
                {
                    long KickID;
                    if (long.TryParse(RecievedMessage.Text.Substring(5), out KickID))
                    {
                        try
                        {
                            Kick(await Bot.GetChatAsync(KickID));
                        }
                        catch
                        { }
                    }
                }
                //Команда на вывод списка пользователей с доступом
                if(RecievedMessage.Text.StartsWith("/list"))
                {
                    await (Bot.SendTextMessageAsync(Admin, ChatsListString));
                }
            }
            //Если написал пользователь без доступа, сообщаем об этом админу
            else if(!Chats.Contains(RecievedMessage.Chat.Id))
            {                
                await Bot.SendTextMessageAsync(Admin, 
                    $"{RecievedMessage.Chat.Username} запросил доступ, пришлите /access{RecievedMessage.Chat.Id}");
                await Bot.SendTextMessageAsync(RecievedMessage.Chat.Id, "Запрос доступа отправлен");
                return;
            }
            //Если пользователь прислал файл
            else if (RecievedMessage.Type == MessageType.DocumentMessage)
            {
                //Получаем файл
                File SourceFile = await Bot.GetFileAsync(RecievedMessage.Document.FileId);
                //Выходной файл имеет то же имя, но с расширением .ak
                string NewFileName = System.IO.Path.ChangeExtension(RecievedMessage.Document.FileName, ".ak");
                System.IO.MemoryStream S = new System.IO.MemoryStream();
                //Подписываем содержимое файла и сохраняем в поток
                SignFile(SourceFile.FileStream).Save(S);
                //Сбрасываем указатель в потоке
                S.Position = 0;
                //Формируем выходной файл
                FileToSend B = new FileToSend(NewFileName, S);
                await Bot.SendDocumentAsync(RecievedMessage.Chat.Id, B);
            }
        }
        /// <summary>
        /// Предоставляем доступ указанному чату
        /// </summary>
        /// <param name="iChat">Чат, которому предоставляется доступ</param>
        internal static async void GiveAccess(Chat iChat)
        {
            //Если у пльзователя уже есть доступ, то выходим
            if (Chats.Contains(iChat.Id))
                return;

            await Bot.SendTextMessageAsync(iChat.Id, "Вам предоставлен доступ");
            //Добавляем ID в список
            Chats.Add(iChat.Id);
            //Сохраняем изменения
            XDocument Doc = XDocument.Load(SAVE_FILE_NAME);
            Doc.Root.Add(new XElement(XML_CHAT, iChat.Id));
            Doc.Save(SAVE_FILE_NAME);
        }
        /// <summary>
        /// Отзывает доступ у указанного чата
        /// </summary>
        /// <param name="iChat">Чат, у которого отзывается доступ</param>
        internal static async void Kick(Chat iChat)
        {
            //Если у пользователя нет доступа, то выходим
            if (!Chats.Contains(iChat.Id))
                return;

            await Bot.SendTextMessageAsync(iChat.Id, "Ваш доступ был аннулирован");
            //Убираем ID из списка
            Chats.Remove(iChat.Id);

            //Сохраняем изменения в файл
            XDocument Doc = XDocument.Load(SAVE_FILE_NAME);
            Doc.Root.Elements(XML_CHAT).Where(x => x.Value == iChat.Id.ToString()).Remove();
            Doc.Save(SAVE_FILE_NAME);
        }
        /// <summary>
        /// Загружает данные из файла
        /// </summary>
        internal static void Load()
        {
            //Загружаем файл
            XDocument Doc = XDocument.Load(SAVE_FILE_NAME);
            //Получаем токен и инициализирем им бота
            Bot = new TelegramBotClient(Doc.Root.Attribute(XML_TOKEN).Value);
            //Получаем ID администратора
            Admin = long.Parse(Doc.Root.Attribute(XML_ADMIN).Value);
            //Получаем ID пользователей
            foreach (XElement ChatElement in Doc.Root.Elements(XML_CHAT))
                Chats.Add(long.Parse(ChatElement.Value));
        }
        /// <summary>
        /// Подписывает файл и возвращает результат в виде XML документа
        /// </summary>
        /// <param name="iStream">Поток, содержащий файл</param>
        /// <returns>XML документ с данными и подписью</returns>
        internal static XDocument SignFile(System.IO.Stream iStream)
        {
            //Создаем буфер
            byte[] BytesToSign = new byte[iStream.Length];
            //Считываем данные из потока
            iStream.Read(BytesToSign, 0, (int)iStream.Length);
            //Получаем подпись
            byte[] SignBytes = CSP.SignData(BytesToSign, new SHA1CryptoServiceProvider());
            //Получаем исходные данные в виде Base64 строки
            string DataString = Convert.ToBase64String(BytesToSign);
            //Получаем подпись в виде Base64 строки
            string SignString = Convert.ToBase64String(SignBytes);
            //Формируем XML файл
            return new XDocument(
                new XElement("signed_data",
                    new XElement("data", DataString),
                    new XElement("sign", SignString)));
        }
    }
}
