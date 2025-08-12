using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public sealed class LobbyUpdateLobby
    {
        public UpdateLobbyCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(out LobbyUpdateLobby op, string lobbyId, Utf8String attrKey, Utf8String attrValue,
            LobbyAttributeVisibility visibility = LobbyAttributeVisibility.Public, float timeout = 30f)
        {
            op = new LobbyUpdateLobby();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbyUpdateLobby] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(op.UpdateLobby(lobbyId, attrKey, attrValue, visibility, timeout));
        }

        private IEnumerator UpdateLobby(string lobbyId, Utf8String attrKey, Utf8String attrValue,
            LobbyAttributeVisibility visibility, float timeout)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(attrKey))
            {
                CallbackInfo = new UpdateLobbyCallbackInfo { ResultCode = Result.InvalidParameters };
                yield break;
            }

            var platform = EOS.GetPlatformInterface();
            if (platform == null)
            {
                CallbackInfo = new UpdateLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var lobby = platform.GetLobbyInterface();

            LobbyModification modification = null;
            var modOpts = new UpdateLobbyModificationOptions
            {
                LobbyId = lobbyId,
                LocalUserId = EOS.LocalProductUserId,
            };
            lobby.UpdateLobbyModification(ref modOpts, out modification);
            if (modification == null)
            {
                CallbackInfo = new UpdateLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var addAttr = new LobbyModificationAddAttributeOptions
            {
                Attribute = new AttributeData
                {
                    Key = attrKey,
                    Value = new AttributeDataValue { AsUtf8 = attrValue }
                },
                Visibility = visibility
            };
            modification.AddAttribute(ref addAttr);

            var upd = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobby.UpdateLobby(ref upd, null, (ref UpdateLobbyCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                timeout,
                () => CallbackInfo = new UpdateLobbyCallbackInfo { ResultCode = Result.TimedOut }
            );

            // ВАЖНО: всегда релизим modification
            modification.Release();
        }
    }
}
