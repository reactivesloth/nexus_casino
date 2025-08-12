using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public sealed class LobbyLeaveLobby
    {
        public LeaveLobbyCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(out LobbyLeaveLobby op, string lobbyId, ProductUserId localUserId, float timeout = 30f)
        {
            op = new LobbyLeaveLobby();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbyLeaveLobby] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(op.LeaveLobby(lobbyId, localUserId, timeout));
        }

        private IEnumerator LeaveLobby(string lobbyId, ProductUserId localUserId, float timeout)
        {
            if (string.IsNullOrEmpty(lobbyId) || localUserId == null)
            {
                CallbackInfo = new LeaveLobbyCallbackInfo { ResultCode = Result.InvalidParameters };
                yield break;
            }

            var platform = EOS.GetPlatformInterface();
            if (platform == null)
            {
                CallbackInfo = new LeaveLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var lobbyInterface = platform.GetLobbyInterface();
            var opts = new LeaveLobbyOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId,
            };

            lobbyInterface.LeaveLobby(ref opts, null,
                (ref LeaveLobbyCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new LeaveLobbyCallbackInfo { ResultCode = Result.TimedOut }
            );
        }
    }
}