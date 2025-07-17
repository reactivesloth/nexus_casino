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
        public DateTime created_at;
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
} 