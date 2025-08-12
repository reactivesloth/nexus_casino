using System;
using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Connect;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;
using Credentials = Epic.OnlineServices.Connect.Credentials;
using LoginCallbackInfo = Epic.OnlineServices.Connect.LoginCallbackInfo;
using LoginOptions = Epic.OnlineServices.Connect.LoginOptions;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class ConnectLogin
    {
        public LoginCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            string id,
            string token,
            string displayName,
            bool automaticallyCreateDeviceId,
            bool automaticallyCreateConnectAccount,
            int timeout,
            AuthScopeFlags scopeFlags,
            out ConnectLogin connectLogin)
        {
            connectLogin = new ConnectLogin();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[ConnectLogin] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(connectLogin.LoginSelectorCoroutine(
                loginCredentialType,
                externalCredentialType,
                id,
                token,
                displayName,
                automaticallyCreateDeviceId,
                automaticallyCreateConnectAccount,
                timeout,
                scopeFlags));
        }

        private IEnumerator LoginSelectorCoroutine(
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            string id,
            string token,
            string displayName,
            bool automaticallyCreateDeviceId,
            bool automaticallyCreateConnectAccount,
            int timeout,
            AuthScopeFlags scopeFlags)
        {
            switch (loginCredentialType)
            {
                case LoginCredentialType.AccountPortal:
                case LoginCredentialType.ExchangeCode:
                case LoginCredentialType.ExternalAuth:
                case LoginCredentialType.Password:
                case LoginCredentialType.PersistentAuth:
                case LoginCredentialType.RefreshToken:
                    yield return Login(token, externalCredentialType, displayName,
                        automaticallyCreateConnectAccount, out var c1, timeout);
                    CallbackInfo = c1?.CallbackInfo;
                    break;

                case LoginCredentialType.Developer:
                    yield return LoginDeveloper(id, token, loginCredentialType, externalCredentialType,
                        scopeFlags, timeout, displayName, automaticallyCreateConnectAccount, out var c2);
                    CallbackInfo = c2?.CallbackInfo;
                    break;

                case LoginCredentialType.DeviceCode:
                    yield return LoginDeviceCode(token, externalCredentialType, displayName,
                        automaticallyCreateConnectAccount, timeout, automaticallyCreateDeviceId, out var c3);
                    CallbackInfo = c3?.CallbackInfo;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(loginCredentialType), loginCredentialType, null);
            }
        }

        private static Coroutine Login(
            string token,
            ExternalCredentialType externalCredentialType,
            string displayName,
            bool automaticallyCreateConnectAccount,
            out ConnectLogin connectLogin,
            int timeout = 30)
        {
            connectLogin = new ConnectLogin();
            var mgr = EOS.GetManager();
            return mgr != null
                ? mgr.StartCoroutine(connectLogin.LoginCoroutine(token, externalCredentialType, displayName, automaticallyCreateConnectAccount, timeout))
                : null;
        }

        private static Coroutine LoginDeveloper(
            string id,
            string token,
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            AuthScopeFlags scopeFlags,
            int timeout,
            string displayName,
            bool automaticallyCreateConnectAccount,
            out ConnectLogin connectLogin)
        {
            connectLogin = new ConnectLogin();
            var mgr = EOS.GetManager();
            return mgr != null
                ? mgr.StartCoroutine(connectLogin.LoginDeveloperCoroutine(id, token, loginCredentialType, externalCredentialType,
                    scopeFlags, timeout, displayName, automaticallyCreateConnectAccount))
                : null;
        }

        private static Coroutine LoginDeviceCode(
            string token,
            ExternalCredentialType externalCredentialType,
            string displayName,
            bool automaticallyCreateConnectAccount,
            int timeout,
            bool automaticallyCreateDeviceId,
            out ConnectLogin connectLogin)
        {
            connectLogin = new ConnectLogin();
            var mgr = EOS.GetManager();
            return mgr != null
                ? mgr.StartCoroutine(connectLogin.LoginDeviceCodeCoroutine(token, externalCredentialType, displayName,
                    automaticallyCreateConnectAccount, timeout, automaticallyCreateDeviceId))
                : null;
        }

        private IEnumerator LoginCoroutine(
            string token,
            ExternalCredentialType externalCredentialType,
            string displayName,
            bool automaticallyCreateConnectAccount,
            int timeout = 30)
        {
            var connect = EOS.GetCachedConnectInterface();
            if (connect == null)
            {
                CallbackInfo = new LoginCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            while (true)
            {
                var loginOptions = new LoginOptions
                {
                    Credentials = new Credentials { Token = token, Type = externalCredentialType },
                };

                if (!string.IsNullOrEmpty(displayName))
                    loginOptions.UserLoginInfo = new UserLoginInfo { DisplayName = displayName };

                connect.Login(ref loginOptions, null, (ref LoginCallbackInfo cb) => { CallbackInfo = cb; });

                yield return new WaitUntilOrTimeout(
                    () => CallbackInfo.HasValue,
                    timeout,
                    () => CallbackInfo = new LoginCallbackInfo { ResultCode = Result.TimedOut }
                );

                if (CallbackInfo?.ResultCode == Result.TimedOut) yield break;
                if (CallbackInfo?.ResultCode != Result.InvalidUser) yield break;
                if (!automaticallyCreateConnectAccount) yield break;

                yield return ConnectCreateUser.Run(CallbackInfo?.ContinuanceToken, timeout, out var createUser);
                if (createUser.CallbackInfo?.ResultCode != Result.Success)
                {
                    CallbackInfo = new LoginCallbackInfo
                    {
                        ResultCode = createUser.CallbackInfo?.ResultCode ?? Result.InvalidAuth
                    };
                    yield break;
                }

                // повторный логин без автосоздания
                automaticallyCreateConnectAccount = false;
            }
        }

        private IEnumerator LoginDeveloperCoroutine(
            string id,
            string token,
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            AuthScopeFlags scopeFlags,
            int timeout,
            string displayName,
            bool automaticallyCreateConnectAccount)
        {
            yield return AuthLogin.Login(id, token, loginCredentialType, externalCredentialType, scopeFlags, timeout, out var authLogin);

            if (authLogin?.CallbackInfo?.ResultCode != Result.Success)
            {
                CallbackInfo = new LoginCallbackInfo
                {
                    ResultCode = authLogin?.CallbackInfo?.ResultCode ?? Result.InvalidAuth
                };
                yield break;
            }

            var auth = EOS.GetCachedAuthInterface();
            if (auth == null)
            {
                CallbackInfo = new LoginCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var copyUserAuthTokenOptions = new CopyUserAuthTokenOptions();
            var copyRes = auth.CopyUserAuthToken(ref copyUserAuthTokenOptions, authLogin.CallbackInfo?.LocalUserId, out var authToken);

            if (copyRes != Result.Success || authToken == null)
            {
                CallbackInfo = new LoginCallbackInfo { ResultCode = copyRes != Result.Success ? copyRes : Result.UnexpectedError };
                yield break;
            }

            var tokenString = authToken.Value.AccessToken;

            // используем и затем освобождаем токен
            yield return Login(tokenString, externalCredentialType, displayName, automaticallyCreateConnectAccount, out var connectLogin, timeout);
            CallbackInfo = connectLogin?.CallbackInfo;
        }

        private IEnumerator LoginDeviceCodeCoroutine(
            string token,
            ExternalCredentialType externalCredentialType,
            string displayName,
            bool automaticallyCreateConnectAccount,
            int timeout,
            bool automaticallyCreateDeviceId)
        {
            yield return Login(token, externalCredentialType, displayName, automaticallyCreateConnectAccount, out var c1, timeout);
            CallbackInfo = c1?.CallbackInfo;

            if (CallbackInfo?.ResultCode != Result.NotFound) yield break;
            if (!automaticallyCreateDeviceId) yield break;

            yield return ConnectCreateDeviceId.Run(timeout, out var createDeviceId);
            if (createDeviceId.CallbackInfo?.ResultCode != Result.Success)
            {
                CallbackInfo = new LoginCallbackInfo
                {
                    ResultCode = createDeviceId.CallbackInfo?.ResultCode ?? Result.InvalidAuth
                };
                yield break;
            }

            yield return Login(token, externalCredentialType, displayName, automaticallyCreateConnectAccount, out var c2, timeout);
            CallbackInfo = c2?.CallbackInfo;
        }
    }
}
