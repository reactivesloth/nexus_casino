﻿using System.Collections;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    public sealed class LobbySearchLobbies
    {
        public LobbySearchFindCallbackInfo? CallbackInfo { get; private set; }
        public LobbyDetails[] LobbyDetailsArray { get; private set; } = System.Array.Empty<LobbyDetails>();

        public static Coroutine Run(out LobbySearchLobbies op, ProductUserId localUserId, uint maxResults = 10)
        {
            op = new LobbySearchLobbies();
            var mgr = EOS.GetManager();
            if (mgr == null)
            {
                Debug.LogError("[LobbySearchLobbies] EOS manager is null.");
                return null;
            }
            return mgr.StartCoroutine(op.SearchLobbiesCoroutine(localUserId, maxResults));
        }

        private IEnumerator SearchLobbiesCoroutine(ProductUserId localUserId, uint maxResults)
        {
            if (localUserId == null) yield break;

            var platform = EOS.GetPlatformInterface();
            if (platform == null) yield break;

            var lobbyInterface = platform.GetLobbyInterface();
            LobbySearch lobbySearch;

            var createOpts = new CreateLobbySearchOptions { MaxResults = maxResults };
            var res = lobbyInterface.CreateLobbySearch(ref createOpts, out lobbySearch);
            if (res != Result.Success || lobbySearch == null) yield break;

            // Простейший фильтр (NAME != "")
            var paramOpts = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Notequal,
                Parameter = new AttributeData
                {
                    Key = "NAME",
                    Value = new AttributeDataValue { AsUtf8 = "" },
                },
            };
            lobbySearch.SetParameter(ref paramOpts);

            var findOpts = new LobbySearchFindOptions { LocalUserId = localUserId };
            lobbySearch.Find(ref findOpts, null, (ref LobbySearchFindCallbackInfo cb) => { CallbackInfo = cb; });

            yield return new WaitUntilOrTimeout(
                () => CallbackInfo.HasValue,
                10f,
                () => CallbackInfo = new LobbySearchFindCallbackInfo { ResultCode = Result.TimedOut }
            );

            if (CallbackInfo?.ResultCode != Result.Success)
            {
                lobbySearch.Release();
                yield break;
            }

            var lobbySearchGetSearchResultCountOptions = new LobbySearchGetSearchResultCountOptions();
            var count = lobbySearch.GetSearchResultCount(ref lobbySearchGetSearchResultCountOptions);
            if (count == 0)
            {
                LobbyDetailsArray = System.Array.Empty<LobbyDetails>();
                lobbySearch.Release();
                yield break;
            }

            LobbyDetailsArray = new LobbyDetails[count];
            for (uint i = 0; i < count; i++)
            {
                var byIdx = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                if (lobbySearch.CopySearchResultByIndex(ref byIdx, out LobbyDetails details) == Result.Success)
                    LobbyDetailsArray[i] = details;
            }

            lobbySearch.Release();
        }
    }
}
