using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Scene.SceneObjectControl
{
    public class SceneObjectsController : MonoBehaviour
    {
        private static ServerManager ServerManager => InstanceFinder.ServerManager;
        private static ClientManager ClientManager => InstanceFinder.ClientManager;
        private static SceneManager SceneManager => InstanceFinder.SceneManager;

        private static readonly Dictionary<string, IControlledSceneObject> _sceneObjects = new();
        private static readonly Dictionary<string, StateMessage> _lastStates = new();

        public static List<IControlledSceneObject> AllSceneObjects => _sceneObjects.Values.ToList();

        private void Awake()
        {
            _sceneObjects.Clear();
            
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IControlledSceneObject>();

            foreach (var controlledSceneObject in components)
            {
                if (!_sceneObjects.TryAdd(controlledSceneObject.Key, controlledSceneObject))
                {
                    Debug.LogWarning($"[SceneControl] Duplicate key: {controlledSceneObject.Key}");
                }
            }
        }

        private void OnEnable()
        {
            if (ClientManager != null)
                ClientManager.RegisterBroadcast<StateMessage>(ClientReceiveState);
            
            if (ServerManager != null)
                ServerManager.RegisterBroadcast<StateMessage>(ServerReceiveState);
            
            if (SceneManager != null)
                SceneManager.OnClientLoadedStartScenes += OnClientConnectionState;
        }

        private void OnDisable()
        {
            if (ClientManager != null)
                ClientManager.UnregisterBroadcast<StateMessage>(ClientReceiveState);
            
            if (ServerManager != null)
                ServerManager.UnregisterBroadcast<StateMessage>(ServerReceiveState);
            
            if (SceneManager != null)
                SceneManager.OnClientLoadedStartScenes -= OnClientConnectionState;
        }

        /// <summary>
        /// Get states by object name.
        /// If function return null object not contains
        /// </summary>
        /// <param name="objectName"></param>
        /// <returns></returns>
        public List<string> GetStatesByName(string objectName) =>
            !_sceneObjects.TryGetValue(objectName, out var sceneObject) ? null : sceneObject.States;
        
        /// <summary>
        /// Local call for command
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="state"></param>
        public void SetState(string objectName, string state)
        {
            if (GetStatesByName(objectName) == null)
                return;
            
            if (ClientManager == null || !ClientManager.Started)
            {
                Debug.LogWarning("[SceneControl] Client not connected");
                return;
            }
            
            var actionMessage = new StateMessage { ObjectName = objectName, Action = state };
            Debug.Log($"[SceneControl] {objectName} is making action {state}");
            
            //Broadcast TO Server
            ClientManager.Broadcast(actionMessage);
        }

        private void OnClientConnectionState(NetworkConnection conn, bool asServer)
        {
            if (!asServer) 
                return;
            
            foreach (var action in _lastStates.Values)
            {
                ServerManager.Broadcast(conn, action);
            }
        }

        private void ServerReceiveState(NetworkConnection conn, StateMessage message,
            Channel channel = Channel.Reliable)
        {
            Debug.Log($"[SceneControl.Server] {message.ObjectName} is making action {message.Action}");
            
            _lastStates[message.ObjectName] = message;
            ServerManager.Broadcast(message);
        }

        private void ClientReceiveState(StateMessage message, Channel channel = Channel.Reliable)
        {
            if (!_sceneObjects.TryGetValue(message.ObjectName, out var sceneObject))
            {
                Debug.LogWarning($"[SceneControl.Client] Object not found: {message.ObjectName}");
                return;
            }
            
            Debug.Log($"[SceneControl.Client] {message.ObjectName} is making action {message.Action}");
            
            _lastStates[message.ObjectName] = message;
            sceneObject.SetState(message.Action);
        }
    }
}