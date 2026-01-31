using System;
using System.Collections.Generic;
using Code.API.Models;
using PurrNet.Packing;

namespace Code.Network
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
    public struct MeSchema : IPackedAuto
    {
        public int id;
        public string username;
        public int balance;
        public DateTime created_at;
        public string role;

        public bool IsAdmin => role == "admin";
        public bool IsHost => role == "host";
        public bool IsModerator => role == "moderator";

        public bool IsAdminRole => IsAdmin || IsHost || IsModerator;

        public static bool CheckAdmin(string role)
        {
            return role is "admin" or "host" or "moderator";
        }
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
        public int lobby_id;
        public int slot_id;
        public string image_url;
        public string created_at;
        public MeSchema user;
    }

    [Serializable]
    public class SendMessageRequest
    {
        public string lobby_id;
        public string message;
        public string type;
    }

    [Serializable]
    public class HistoryEnvelopeData
    {
        public MessageData[] messages;
        public int total_count;
        public bool has_more;
    }

    [Serializable]
    public class BanData
    {
        public string username;
        public int timeout_minutes = 0;
    }

    [Serializable]
    public class TopSchema
    {
        public List<TopRecord> records;
        public string period;
    }

    [Serializable]
    public class TopRecord
    {
        public int user_id;
        public string username;
        public int total_amount;
        public int deposits_count;
        public int withdrawals_count;
    }

    [Serializable]
    public class CheckUsernameRequest
    {
        public string username;
    }

    [Serializable]
    public class InteractableSocialData
    {
        public string object_id;

        public int likes_count;
        public bool is_liked_by_me;

        public int views_count;
        public bool is_viewed_by_me;
    }

    [Serializable]
    public class InteractableSocialRequest
    {
        public string object_id;
    }

    [Serializable]
    public class ToggleLikeRequest : InteractableSocialRequest
    {
        public bool like;
    }
}