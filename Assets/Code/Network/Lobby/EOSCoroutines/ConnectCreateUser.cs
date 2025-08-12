using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public class ConnectCreateUser
    {
        public CreateUserCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(ContinuanceToken continuanceToken, int timeout, out ConnectCreateUser connectCreateUser)
        {
            connectCreateUser = new ConnectCreateUser();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[ConnectCreateUser] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(connectCreateUser.CreateUserCoroutine(continuanceToken, timeout));
        }

        private IEnumerator CreateUserCoroutine(ContinuanceToken continuanceToken, int timeout)
        {
            var connect = EOS.GetCachedConnectInterface();
            if (connect == null)
            {
                CallbackInfo = new CreateUserCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var createUserOptions = new CreateUserOptions { ContinuanceToken = continuanceToken };

            connect.CreateUser(ref createUserOptions, null, (ref CreateUserCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new CreateUserCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}