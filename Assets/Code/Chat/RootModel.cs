using System;
using Newtonsoft.Json;

namespace Code.Chat
{
    [Serializable]
    public class RootModel
    {
        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("data")]
        public MessageData Data { get; set; }
    }

    [Serializable]
    public class MessageData
    {
        [JsonProperty("message")]
        public MessageInfo Message { get; set; }
    }

    [Serializable]
    public class MessageInfo
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("lobby_id")]
        public string LobbyId { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAtRaw { get; set; }

        [JsonIgnore]
        public DateTime CreatedAt => DateTime.Parse(CreatedAtRaw, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}