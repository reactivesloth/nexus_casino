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
            ClientManager.RegisterBroadcast<StateMessage>(MakeAction);
            ServerManager.RegisterBroadcast<StateMessage>(ServerReceiveAction);
            SceneManager.OnClientLoadedStartScenes += OnClientConnectionState;
        }

        private void OnDisable()
        {
            ClientManager.UnregisterBroadcast<StateMessage>(MakeAction);
            ServerManager.UnregisterBroadcast<StateMessage>(ServerReceiveAction);
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
        public void MakeAction(string objectName, string state)
        {
            if (GetStatesByName(objectName) == null)
                return;
            var actionMessage = new StateMessage { ObjectName = objectName, Action = state };
            if (ServerManager.Started)
                ServerManager.Broadcast(actionMessage);
            else if (ClientManager.Started)
                ClientManager.Broadcast(actionMessage);
        }

        private void OnClientConnectionState(NetworkConnection conn, bool asServer)
        {
            if (!asServer) return;
            foreach (var action in _lastStates.Values)
            {
                ServerManager.Broadcast(conn, action);
            }
        }

        private void ServerReceiveAction(NetworkConnection conn, StateMessage message,
            Channel channel = Channel.Reliable)
        {
            _lastStates[message.ObjectName] = message;
            ServerManager.Broadcast(message);
        }

        private void MakeAction(StateMessage message, Channel channel = Channel.Reliable)
        {
            if (!_sceneObjects.TryGetValue(message.ObjectName, out var sceneObject))
                return;
            _lastStates[message.ObjectName] = message;
            sceneObject.SetState(message.Action);
        }
    }
}