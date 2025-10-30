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
    public class Empty
    {
    }

    // NB: 'SendMassage' — сохранено как в исходном коде для совместимости
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
        public long id;
        public string lobby_id;
        public int user_id;
        public string type;
        public string message;
        public MeSchema user;
        public string created_at;
        public int likes_count;
        public bool is_liked_by_me;
        public int views_count;
        public bool is_viewed_by_me;
    }

    [Serializable]
    public class Error
    {
        public string message;
    }

    [Serializable]
    public class ReactBaseModel
    {
        public long message_id;
    }

    [Serializable]
    public class ViewUpdatedModel: ReactBaseModel
    {
        public int views_count;
        public bool is_viewed_by_me;
    }

    [Serializable]
    public class LikeUpdatedModel: ReactBaseModel
    {
        public int likes_count;
        public bool is_liked_by_me;
    }

    [Serializable]
    public class LikeSendModel : ReactBaseModel
    {
        public bool like;
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

        public const string ToggleLike = "toggle_like";
        public const string LikeToggled = "like_toggled";
        public const string MessageLikeUpdated = "message_like_updated";

        public const string MarkViewed = "mark_viewed";
        public const string ViewMarked = "view_marked";
        public const string MessageViewsUpdated = "message_views_updated";
    }
}