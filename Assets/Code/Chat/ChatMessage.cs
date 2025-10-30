using System;
using Code.API.Models;
using JetBrains.Annotations;

namespace Code.Chat
{
    /// <summary>
    /// Структура сообщения в чате
    /// </summary>
    [Serializable]
    public class ChatMessage
    {
        public string displayUsername;
        public string displayMessage;
        public ChatType chatType;
        public DateTime timestamp = DateTime.Now;
        public ChatMessageStyle style = ChatStyles.Default;
        
        [CanBeNull] public MessageData chatMessageData;
    }
}