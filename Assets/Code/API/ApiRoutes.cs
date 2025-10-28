using Epic.OnlineServices.Stats;

namespace Code.API
{
    public static class ApiRoutes
    {
        public const string DOMAIN = "https://back.nexusmetaclub.com";

        public const string CHECK_NUMBER = "/api/client/auth/checkPhone";
        public const string CHECK_USERNAME = "/api/client/auth/checkUsername";
        public const string SIGN_UP = "/api/client/auth/signUp";
        public const string LOGIN = "/api/client/auth/login";
        public const string SEND_CODE = "/api/client/auth/sendCode";
        public const string GET_ME = "/api/client/users/me";
        public const string LOAD_STORY = "/api/client/screenshots/add";
        public const string GET_STRORIES_URL = "/api/client/screenshots/";
        public const string GET_INTERACTABLE_SOCIAL_URL = "/api/client/lobby/interactive-objects/likes-count";
        
        public const string LOAD_FILE_URL = "/api/s3/upload";
        public const string GET_FILE_URL = "/api/s3/{0}";
        
        public const string BAN_URL = "/api/client/commands/ban";
        public const string UNBAN_URL = "/api/client/commands/unban";
        
        public static string GetCheckNumberUrl() => DOMAIN.TrimEnd('/') + CHECK_NUMBER;
        public static string GetSignUpUrl() => DOMAIN.TrimEnd('/') + SIGN_UP;
        public static string GetLoginUrl() => DOMAIN.TrimEnd('/') + LOGIN;
        public static string GetSendCodeUrl() => DOMAIN.TrimEnd('/') + SEND_CODE;
        public static string GetMeUrl() => DOMAIN.TrimEnd('/') + GET_ME;
        public static string GetLoadStoryUrl() => DOMAIN.TrimEnd('/') + LOAD_STORY;
        public static string GetStoriesUrl() => DOMAIN.TrimEnd('/') + GET_STRORIES_URL;
        public static string GetInteractableSocialUrl() => DOMAIN.TrimEnd('/') + GET_INTERACTABLE_SOCIAL_URL;
        
        public static string GetLoadFileUrl() => DOMAIN.TrimEnd('/') + LOAD_FILE_URL;
        public static string GetFileUrl(string key) => DOMAIN.TrimEnd('/') + string.Format(GET_FILE_URL, key);

        public static string GetBanUrl() => DOMAIN.TrimEnd('/') + BAN_URL;
        public static string GetUnbanUrl() => DOMAIN.TrimEnd('/') + UNBAN_URL;

        public static string CheckNickNameUrl => DOMAIN.TrimEnd('/') + CHECK_USERNAME;
    }
}
