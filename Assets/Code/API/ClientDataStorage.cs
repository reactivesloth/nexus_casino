using System.Collections.Generic;
using Code.Network;

namespace Code.API
{
    public static class ClientDataStorage
    {
        public static string AccessToken { get; set; }
        public static string RefreshToken { get; set; }

        public const string JwtHeaderName = "Jwt";

        public static MeSchema UserData { get; set; }

        public static Dictionary<string, string> GetJwtHeader(bool useRefresh = false)
        {
            return new Dictionary<string, string>(1)
            {
                { JwtHeaderName, useRefresh ? (RefreshToken ?? string.Empty) : (AccessToken ?? string.Empty) }
            };
        }
    }
}