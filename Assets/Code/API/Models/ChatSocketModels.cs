using System;

namespace Code.API.Models
{
    [Serializable]
    public class ChatModel<T>
    {
        public string @event;
        public T data;
    }
    
    [Serializable]
    public class Empty{}

    [Serializable]
    public class SendMassage
    {
        public string lobby_id = "main";
        public string message;
        public string type;
    }
    
    [Serializable]
    public class SendMassageSuccess
    {
        public int message_id;
    }

    [Serializable]
    public class SendImportantMessage
    {
        public string message;
    }

    [Serializable]
    public class NewMessageData
    {
        public MessageData message;
    }
    
    [Serializable]
    public class MessageData
    {
        public int id;
        public string lobby_id;
        public string message;
        public string type;

        public MeSchema user;
        
        public int user_id;
        
    }
    
    [Serializable]
    public class Error
    {
        public string message;
    }
    
    public static class ChatSocketEvents
    {
        public const string Ping = "ping";
        public const string Pong = "pong";
        public const string SendMessage = "send_message";
        public const string MessageSent = "message_sent";
        public const string Error = "error";
        public const string SendImportantMessage = "send_important_message";
        public const string ImportantMessageSent = "important_message_sent";
        public const string NewMessage = "new_message";
        public const string NewImportantMessage = "new_important_message";
    }
}