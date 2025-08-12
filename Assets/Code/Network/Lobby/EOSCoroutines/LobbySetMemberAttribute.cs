using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public sealed class LobbySetMemberAttribute
    {
        public UpdateLobbyCallbackInfo? CallbackInfo { get; private set; }

        public static Coroutine Run(out LobbySetMemberAttribute op, string lobbyId, ProductUserId localUserId,
            string attrKey, string attrValue, LobbyAttributeVisibility visibility = LobbyAttributeVisibility.Public,
            float timeout = 30f)
        {
            op = new LobbySetMemberAttribute();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbySetMemberAttribute] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(op.SetMemberAttributeCoroutine(lobbyId, localUserId, attrKey, attrValue, visibility, timeout));
        }

        private IEnumerator SetMemberAttributeCoroutine(string lobbyId, ProductUserId localUserId,
            string attrKey, string attrValue, LobbyAttributeVisibility visibility, float timeout)
        {
            if (string.IsNullOrEmpty(lobbyId) || localUserId == null || string.IsNullOrEmpty(attrKey))
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
                LocalUserId = localUserId,
            };
            lobby.UpdateLobbyModification(ref modOpts, out modification);
            if (modification == null)
            {
                CallbackInfo = new UpdateLobbyCallbackInfo { ResultCode = Result.UnexpectedError };
                yield break;
            }

            var addMemberAttr = new LobbyModificationAddMemberAttributeOptions
            {
                Attribute = new AttributeData
                {
                    Key = attrKey,
                    Value = new AttributeDataValue { AsUtf8 = attrValue ?? string.Empty }
                },
                Visibility = visibility
            };
            modification.AddMemberAttribute(ref addMemberAttr);

            var updOpts = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobby.UpdateLobby(ref updOpts, null, (ref UpdateLobbyCallbackInfo cb) => { CallbackInfo = cb; });

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
