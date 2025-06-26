using UnityEngine;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;

namespace Code.Network
{
    public class HostSessionRestorer : MonoBehaviour
    {
        public static HostSessionRestorer Instance;

        private void Awake() => Instance = this;

        public void RestorePlayerState(PlayerSessionState state, NetworkConnection conn)
        {
            foreach (var objState in state.Objects)
            {
                if (objState.IsSceneObject)
                {
                    // Найти объект на сцене по SceneObjectId
                    var sceneObj = GameObject.Find(objState.SceneObjectId);
                    if (sceneObj == null)
                    {
                        Debug.LogWarning($"Scene object {objState.SceneObjectId} not found!");
                        continue;
                    }

                    var networkObject = sceneObj.GetComponent<NetworkObject>();
                    if (networkObject != null)
                    {
                        networkObject.GiveOwnership(conn);
                        sceneObj.transform.position = objState.Position;
                        sceneObj.transform.rotation = objState.Rotation;
                    }
                }
                else
                {
                    var prefab = InstanceFinder.NetworkManager.SpawnablePrefabs.GetObject(true, objState.PrefabId);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"Prefab {objState.PrefabId} not found!");
                        continue;
                    }

                    var netObj = Instantiate(prefab, objState.Position, objState.Rotation);
                    var networkObject = netObj.GetComponent<NetworkObject>();
                    if (networkObject != null)
                    {
                        InstanceFinder.ServerManager.Spawn(networkObject, conn);
                    }
                }
            }
        }

        public void RestorePlayerCharacterState(PlayerCharacterState state, NetworkConnection sender)
        {
            //TODO: find player without check tag
            var playerCharacterNetworkObject = sender.Objects.FirstOrDefault(o => o.CompareTag("Player"));
            if (playerCharacterNetworkObject == null)
            {
                Debug.LogError($"[HostSessionRestorer] PlayerCharacterState not found for {sender}");
                return;
            }
            
            playerCharacterNetworkObject.transform.position = state.Position;
            playerCharacterNetworkObject.transform.rotation = state.Rotation;
        }
    }
}