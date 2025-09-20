using System;
using System.Collections.Generic;
using System.Linq;
using Code.API.Models;
using Code.Network.Lobby.Data;
using UnityEngine;

namespace Code.Network.Lobby
{
    public static class NewHostAutoSelector
    {
        private const long GoodPing = 300;

        // веса влияния (можно настроить под проект)
        private const float PingWeight = 0.5f;      // чем больше, тем важнее пинг
        private const float HardwareWeight = 0.5f;  // чем больше, тем важнее железо

        public static string GetNewHostIdAuto(bool includeMe)
        {
            var members = LobbyVariables.Instance.currentLobby.lobbyMembers;
            if (!includeMe)
            {
                var meMember = members.FirstOrDefault(m => m.productUserId == LobbyVariables.Instance.productUserId);
                if(meMember != null)
                    members.Remove(meMember);
            }
            
            var adminMembers = members
                .Where(m => m.Attributes.TryGetValue("ROLE", out var role) && MeSchema.CheckAdmin(role))
                .ToList();

            Debug.Log($"[HostMigration] Admins {adminMembers.Count}");
            
            if (adminMembers.Count > 0)
                return SelectWithCombinedScore(adminMembers);

            var goodPingMembers = members
                .Where(m => m.Attributes.TryGetValue("PING", out var ping)
                            && long.TryParse(ping, out var pingValue)
                            && pingValue <= GoodPing)
                .ToList();

            if (goodPingMembers.Count == 0)
                return SelectWithCombinedScore(members);

            return SelectWithCombinedScore(goodPingMembers);
        }

        private static string SelectWithCombinedScore(List<LobbyData.LobbyMember> members)
        {
            // Собираем максимальные значения для нормализации
            var pings = members
                .Select(m => TryGetLong(m.Attributes, "PING"))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            var hardwares = members
                .Select(m => TryGetLong(m.Attributes, "HARDWARE_SCORE"))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            long maxPing = pings.Count > 0 ? pings.Max() : 1;
            long maxHardware = hardwares.Count > 0 ? hardwares.Max() : 1;

            var scoredMembers = members
                .Select(m =>
                {
                    var ping = TryGetLong(m.Attributes, "PING") ?? maxPing;  // если нет — считаем худшим
                    var hw = TryGetLong(m.Attributes, "HARDWARE_SCORE") ?? 0; // если нет — минимальное железо

                    // Нормализация [0..1]
                    float pingNorm = (float)ping / maxPing;
                    float hwNorm = (float)hw / maxHardware;

                    // Чем меньше результат, тем лучше
                    float score = PingWeight * pingNorm - HardwareWeight * hwNorm;

                    return new { Member = m, Score = score };
                })
                .OrderBy(x => x.Score) // минимальный Score — лучший
                .ToList();

            return scoredMembers.Count > 0
                ? scoredMembers.First().Member.productUserId
                : members.First().productUserId;
        }

        private static long? TryGetLong(Dictionary<string, string> attrs, string key)
        {
            if (attrs.TryGetValue(key, out var val) && long.TryParse(val, out var result))
                return result;
            return null;
        }
    }
}
