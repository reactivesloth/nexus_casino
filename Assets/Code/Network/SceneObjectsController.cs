using System.Collections.Generic;
using System.Linq;
using Code.Scene.SceneObjectControl;
using PurrNet;
using PurrNet.Modules;
using UnityEngine;

namespace Code.Network
{
    public class SceneObjectsController : MonoBehaviour
    {
        private static readonly Dictionary<string, IControlledSceneObject> SceneObjects = new();
        private static readonly Dictionary<string, StateMessage> LastStates = new();

        public static List<IControlledSceneObject> AllSceneObjects => SceneObjects.Values.ToList();

        private void Awake()
        {
            SceneObjects.Clear();

            var components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IControlledSceneObject>();

            foreach (var controlledSceneObject in components)
            {
                if (!SceneObjects.TryAdd(controlledSceneObject.Key, controlledSceneObject))
                {
                    Debug.LogWarning($"[SceneControl] Duplicate key: {controlledSceneObject.Key}");
                }
            }
        }

        private void OnEnable()
        {
            var networkManager = InstanceHandler.NetworkManager;
            if (networkManager != null)
            {
                // Сервер слушает сообщения от клиентов
                networkManager.Subscribe<StateMessage>(ServerReceiveState, true);
            
                // Клиент слушает сообщения от сервера
                networkManager.Subscribe<StateMessage>(ClientReceiveState, false);

                // 2. События сцены (аналог OnClientLoadedStartScenes)
                // В PurrNet нужно получить модуль ScenePlayersModule
                if (networkManager.TryGetModule(out ScenePlayersModule scenePlayers, true))
                {
                    scenePlayers.onPlayerLoadedScene += OnPlayerLoadedScene_PurrNet;
                }
            }
        }

        private void OnDisable()
        {
            var networkManager = InstanceHandler.NetworkManager;
            if (networkManager != null)
            {
                // Отписка от Broadcast
                networkManager.Unsubscribe<StateMessage>(ServerReceiveState, true);
                networkManager.Unsubscribe<StateMessage>(ClientReceiveState, false);

                // Отписка от событий сцены
                if (networkManager.TryGetModule(out ScenePlayersModule scenePlayers, true))
                {
                    scenePlayers.onPlayerLoadedScene -= OnPlayerLoadedScene_PurrNet;
                }
            }
        }
        
        /// <summary>
        /// Get states by object name.
        /// If function return null object not contains
        /// </summary>
        /// <param name="objectName"></param>
        /// <returns></returns>
        public List<string> GetStatesByName(string objectName) =>
            !SceneObjects.TryGetValue(objectName, out var sceneObject) ? null : sceneObject.States;

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

            //Broadcast TO Server
            NetworkManager.main.SendToServer(actionMessage);
        }

        private void OnPlayerLoadedScene_PurrNet(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer)
                return;

            foreach (var action in LastStates.Values)
            {
                NetworkManager.main.Send(player, action);
            } 
        }

        private void ServerReceiveState(PlayerID conn, StateMessage message, bool asServer)
        {
            Debug.Log($"[SceneControl.Server] {message.ObjectName} is making action {message.Action}");

            LastStates[message.ObjectName] = message;
            NetworkManager.main.SendToAll(message);
        }

        private void ClientReceiveState(PlayerID player, StateMessage message, bool asServer)
        {
            if (!SceneObjects.TryGetValue(message.ObjectName, out var sceneObject))
            {
                Debug.LogWarning($"[SceneControl.Client] Object not found: {message.ObjectName}");
                return;
            }

            Debug.Log($"[SceneControl.Client] {message.ObjectName} is making action {message.Action}");

            LastStates[message.ObjectName] = message;
            sceneObject.SetState(message.Action);
        }
    }
}