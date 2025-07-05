using System.Collections.Generic;
using Code.API.Models;

namespace Code.API
{
    public static class ClientDataStorage
    {
        public static string AccessToken { get; set; }
        public static string RefreshToken { get; set; }

        public const string JwtHeaderName = "Jwt";

        public static MeSchema UserData { get; set; }

        /// <summary>
        /// Получить заголовок для запроса с нужным токеном
        /// </summary>
        /// <param name="useRefresh">true — использовать refresh токен, иначе access</param>
        /// <returns>Словарь с заголовком Jwt</returns>
        public static Dictionary<string, string> GetJwtHeader(bool useRefresh = false)
        {
            return new Dictionary<string, string>
            {
                { JwtHeaderName, useRefresh ? RefreshToken : AccessToken }
            };
        }
    }
} 