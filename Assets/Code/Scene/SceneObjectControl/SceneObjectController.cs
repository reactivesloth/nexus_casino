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
    public class SceneObjectController : MonoBehaviour
    {
        private static ServerManager ServerManager => InstanceFinder.ServerManager;
        private static ClientManager ClientManager => InstanceFinder.ClientManager;
        private static SceneManager SceneManager => InstanceFinder.SceneManager;

        private static readonly Dictionary<string, IControlledSceneObject> _sceneObjects = new();
        private static readonly Dictionary<string, StateMessage> _lastStates = new();

        public static List<IControlledSceneObject> AllSceneObjects => _sceneObjects.Values.ToList();

        private void Awake()
        {
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IControlledSceneObject>();

            foreach (var controlledSceneObject in components)
            {
                _sceneObjects.Add(controlledSceneObject.Key, controlledSceneObject);
            }
        }

        private void OnEnable()
        {
            ClientManager.RegisterBroadcast<StateMessage>(ClientReceiveState);
            ServerManager.RegisterBroadcast<StateMessage>(ServerReceiveState);
            SceneManager.OnClientLoadedStartScenes += OnClientConnectionState;
        }

        private void OnDisable()
        {
            ClientManager.UnregisterBroadcast<StateMessage>(ClientReceiveState);
            ServerManager.UnregisterBroadcast<StateMessage>(ServerReceiveState);
            SceneManager.OnClientLoadedStartScenes += OnClientConnectionState;
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
            
            var actionMessage = new StateMessage { ObjectName = objectName, Action = state };
            Debug.Log($"[SceneControl] {objectName} is making action {state}");
            
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
                return;
            
            Debug.Log($"[SceneControl.Client] {message.ObjectName} is making action {message.Action}");
            
            _lastStates[message.ObjectName] = message;
            sceneObject.SetState(message.Action);
        }
    }
}