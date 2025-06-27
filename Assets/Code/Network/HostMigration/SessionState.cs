using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using FishNet.Object;
using UnityEngine;

namespace Code.Network
{
    [Serializable]
    public class SessionState
    {
        public List<PlayerSessionState> Players = new List<PlayerSessionState>();
    }

    [Serializable]
    public class PlayerSessionState
    {
        public int ClientId;
        public List<NetworkObjectState> Objects = new List<NetworkObjectState>();
    }

    [Serializable]
    public class NetworkObjectState
    {
        public ushort PrefabId = NetworkObject.UNSET_PREFABID_VALUE;
        public int ObjectId;
        public bool IsSceneObject;
        public string SceneObjectId;
        public Vector3 Position;
        public Quaternion Rotation;
        // Добавьте другие нужные поля
    }

    [Serializable]
    public class PlayerCharacterState
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }
} 