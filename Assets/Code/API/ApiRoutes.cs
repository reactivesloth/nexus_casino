namespace Code.API
{
    public static class ApiRoutes
    {
        public const string DOMAIN = "https://back.nexusmetaclub.com";

        public const string CHECK_NUMBER = "/api/client/auth/checkPhone";
        public const string SIGN_UP = "/api/client/auth/signUp";
        public const string LOGIN = "/api/client/auth/login";
        public const string UPDATE_TOKENS = "/api/client/auth/updateTokens";
        public const string SEND_CODE = "/api/client/auth/sendCode";
        public const string GET_ME = "/api/client/users/me";
        public const string GET_OPERATORS = "/api/client/operators/";
        public const string GET_OPERATOR_LOGIN_URL = "/api/client/operators/{0}/loginUrl";
        public const string LOAD_FILE_URL = "/api/s3/upload";
        public const string LOAD_STORY = "/api/client/screenshots/add";
        public const string GET_STRORIES_URL = "/api/client/screenshots/";
        public const string SEND_MESSAGE_URL = "/api/client/lobby-messages/send";

        public static string GetCheckNumberUrl() => DOMAIN.TrimEnd('/') + CHECK_NUMBER;
        public static string GetSignUpUrl() => DOMAIN.TrimEnd('/') + SIGN_UP;
        public static string GetLoginUrl() => DOMAIN.TrimEnd('/') + LOGIN;
        public static string GetUpdateTokensUrl() => DOMAIN.TrimEnd('/') + UPDATE_TOKENS;
        public static string GetSendCodeUrl() => DOMAIN.TrimEnd('/') + SEND_CODE;
        public static string GetMeUrl() => DOMAIN.TrimEnd('/') + GET_ME;
        public static string GetOperatorsUrl() => DOMAIN.TrimEnd('/') + GET_OPERATORS;
        public static string GetOperatorLoginUrl(int operatorId) => DOMAIN.TrimEnd('/') + string.Format(GET_OPERATOR_LOGIN_URL, operatorId);
        public static string GetLoadFileUrl() => DOMAIN.TrimEnd('/') + LOAD_FILE_URL;
        public static string GetLoadStoryUrl() => DOMAIN.TrimEnd('/') + LOAD_STORY;
        public static string GetStoriesUrl() => DOMAIN.TrimEnd('/') + GET_STRORIES_URL;
        public static string SendMessageUrl() => DOMAIN.TrimEnd('/') + SEND_MESSAGE_URL;
    }
}
