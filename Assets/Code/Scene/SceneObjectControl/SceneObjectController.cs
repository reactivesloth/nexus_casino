using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Scene.SceneObjectControl
{
    public class SceneObjectController : MonoBehaviour
    {
        private ServerManager ServerManager => InstanceFinder.ServerManager;
        private ClientManager ClientManager => InstanceFinder.ClientManager;
        
        private readonly Dictionary<string, IControlledSceneObject> _sceneObjects = new();

        private void Awake()
        {
            var components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IControlledSceneObject>();

            foreach (var controlledSceneObject in components)
            {
                _sceneObjects.Add(controlledSceneObject.Name, controlledSceneObject);
            }
        }

        private void OnEnable()
        {
            ClientManager.RegisterBroadcast<ActionMessage>(MakeAction);
            ServerManager.RegisterBroadcast<ActionMessage>(ServerReceiveAction);
        }

        private void OnDisable()
        {
            ClientManager.UnregisterBroadcast<ActionMessage>(MakeAction);
            ServerManager.UnregisterBroadcast<ActionMessage>(ServerReceiveAction);
        }

        public bool IsObjectExist(string objectName) => _sceneObjects.ContainsKey(objectName);

        /// <summary>
        /// Local call for command
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="action"></param>
        public void MakeAction(string objectName, string action)
        {
            if(!IsObjectExist(objectName))
                return;
            var actionMessage = new ActionMessage{ObjectName = objectName, Action = action};
            if(ServerManager.Started)
                ServerManager.Broadcast(actionMessage);
            else if(ClientManager.Started)
                ClientManager.Broadcast(actionMessage);
        }

        private void ServerReceiveAction(NetworkConnection conn, ActionMessage actionMessage,
            Channel channel = Channel.Reliable)
        {
            ServerManager.Broadcast(actionMessage);
        }

        private void MakeAction(ActionMessage message, Channel channel = Channel.Reliable)
        {
            if(!_sceneObjects.TryGetValue(message.ObjectName, out var sceneObject))
                return;
            sceneObject.Action(message.Action);
        }
    }
}