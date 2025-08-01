// Папка: Models
// Все модели данных по openapi.json

using System;
using System.Collections.Generic;

namespace Code.API.Models
{
    [Serializable]
    public class SignUpRequest
    {
        public string username;
        public string phone;
        public string confirmation_code;
    }

    [Serializable]
    public class AuthResponse
    {
        public string access_jwt;
        public string refresh_jwt;
    }

    [Serializable]
    public class LoginRequest
    {
        public string phone;
        public string confirmation_code;
    }

    [Serializable]
    public class SendCodeRequest
    {
        public string phone;
        public string requested_by;
    }

    [Serializable]
    public class MeSchema
    {
        public int id;
        public string username;
        public int balance;
        public DateTime created_at;
        public string role;
    }

    [Serializable]
    public class OperatorSchema
    {
        public int id;
        public string name;
    }

    [Serializable]
    public class GetOperatorsResponse
    {
        public List<OperatorSchema> operators;
        public int total_count;
    }

    [Serializable]
    public class SuccessResponse<T>
    {
        public bool success = true;
        public T data;

        // for success = false
        public string code;
        public string traceback;
        public string detail;
    }

    [Serializable]
    public class HTTPValidationError
    {
        public List<ValidationError> detail;
    }

    [Serializable]
    public class ValidationError
    {
        public List<object> loc;
        public string msg;
        public string type;
    }

    [Serializable]
    public class CheckPhoneRequest
    {
        public string phone;
    }

    [Serializable]
    public class PostStoryData
    {
        public string image_url;
        public int slot_id;

        public string lobby_id;
        //public int lobby_id;
    }

    [Serializable]
    public class StoryCollection
    {
        public List<GetStoryData> screenshots;
    }

    [Serializable]
    public class GetStoryData
    {
        public int id;

        public int user_id;

        //public string lobby_id;
        public int lobby_id;
        public int slot_id;
        public string image_url;
        public string created_at;
        public MeSchema user;
    }

    [Serializable]
    public class MessageData
    {
        public string username;
        public string lobby;
        public int type;
        public string text;
    }

    [Serializable]
    public class SendMessageRequest
    {
        public string lobby_id;
        public string message;
        public string type;
    }
}