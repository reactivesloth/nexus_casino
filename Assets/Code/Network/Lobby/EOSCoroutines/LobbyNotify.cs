﻿using System;
using System.Collections.Generic;
using Epic.OnlineServices.Lobby;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    /// <summary>
    /// Безопасная регистрация/снятие EOS-нотификаций. Защита от дублей ключей.
    /// </summary>
    public static class LobbyNotify
    {
        private static readonly Dictionary<string, ulong> _handles = new Dictionary<string, ulong>(128);

        private static string MakeKey(string prefix, Delegate cb)
        {
            if (cb == null) return prefix + "-NULL";
            string target = cb.Target != null ? cb.Target.GetHashCode().ToString() : "static";
            return $"{prefix}-{target}-{cb.Method.Name}";
        }

        public static void AddNotifyLobbyMemberUpdateReceived(Action<LobbyMemberUpdateReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyMemberUpdateReceived", callback);
            if (_handles.ContainsKey(key)) return;

            var opt = new AddNotifyLobbyMemberUpdateReceivedOptions();
            var lobby = platform.GetLobbyInterface();
            ulong handle = lobby.AddNotifyLobbyMemberUpdateReceived(ref opt, null,
                (ref LobbyMemberUpdateReceivedCallbackInfo info) => callback?.Invoke(info));

            _handles[key] = handle;
            Debug.Log($"[LobbyNotify] Add {key} -> {handle}");
        }

        public static void AddNotifyLobbyMemberStatusReceived(Action<LobbyMemberStatusReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyMemberStatusReceived", callback);
            if (_handles.ContainsKey(key)) return;

            var opt = new AddNotifyLobbyMemberStatusReceivedOptions();
            var lobby = platform.GetLobbyInterface();
            ulong handle = lobby.AddNotifyLobbyMemberStatusReceived(ref opt, null,
                (ref LobbyMemberStatusReceivedCallbackInfo info) => callback?.Invoke(info));

            _handles[key] = handle;
            Debug.Log($"[LobbyNotify] Add {key} -> {handle}");
        }

        public static void AddNotifyLobbyUpdateReceived(Action<LobbyUpdateReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyUpdateReceived", callback);
            if (_handles.ContainsKey(key)) return;

            var opt = new AddNotifyLobbyUpdateReceivedOptions();
            var lobby = platform.GetLobbyInterface();
            ulong handle = lobby.AddNotifyLobbyUpdateReceived(ref opt, null,
                (ref LobbyUpdateReceivedCallbackInfo info) => callback?.Invoke(info));

            _handles[key] = handle;
            Debug.Log($"[LobbyNotify] Add {key} -> {handle}");
        }

        public static void RemoveNotifyLobbyMemberUpdateReceived(Action<LobbyMemberUpdateReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyMemberUpdateReceived", callback);
            if (!_handles.TryGetValue(key, out ulong handle)) return;

            platform.GetLobbyInterface()?.RemoveNotifyLobbyMemberUpdateReceived(handle);
            _handles.Remove(key);
            Debug.Log($"[LobbyNotify] Remove {key} -> {handle}");
        }

        public static void RemoveNotifyLobbyMemberStatusReceived(Action<LobbyMemberStatusReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyMemberStatusReceived", callback);
            if (!_handles.TryGetValue(key, out ulong handle)) return;

            platform.GetLobbyInterface()?.RemoveNotifyLobbyMemberStatusReceived(handle);
            _handles.Remove(key);
            Debug.Log($"[LobbyNotify] Remove {key} -> {handle}");
        }

        public static void RemoveNotifyLobbyUpdateReceived(Action<LobbyUpdateReceivedCallbackInfo> callback)
        {
            var platform = EOS.GetPlatformInterface();
            if (platform == null) return;

            string key = MakeKey("LobbyUpdateReceived", callback);
            if (!_handles.TryGetValue(key, out ulong handle)) return;

            platform.GetLobbyInterface()?.RemoveNotifyLobbyUpdateReceived(handle);
            _handles.Remove(key);
            Debug.Log($"[LobbyNotify] Remove {key} -> {handle}");
        }
    }
}
