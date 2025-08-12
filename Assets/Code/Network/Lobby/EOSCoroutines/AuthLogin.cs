﻿using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class AuthLogin
    {
        public LoginCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Login(
            string id,
            string token,
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            AuthScopeFlags scopeFlags,
            int timeout,
            out AuthLogin authLogin)
        {
            authLogin = new AuthLogin();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[AuthLogin] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(authLogin.LoginCoroutine(id, token, loginCredentialType, externalCredentialType, scopeFlags, timeout));
        }

        private IEnumerator LoginCoroutine(
            string id,
            string token,
            LoginCredentialType loginCredentialType,
            ExternalCredentialType externalCredentialType,
            AuthScopeFlags scopeFlags,
            int timeout)
        {
            var auth = EOS.GetCachedAuthInterface();
            if (auth == null)
            {
                CallbackInfo = new LoginCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var loginOptions = new LoginOptions
            {
                Credentials = new Credentials
                {
                    Id = id,
                    Token = token,
                    Type = loginCredentialType,
                    SystemAuthCredentialsOptions = default,
                    ExternalType = externalCredentialType
                },
                ScopeFlags = scopeFlags
            };

            auth.Login(ref loginOptions, null, (ref LoginCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new LoginCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}
