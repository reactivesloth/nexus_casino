using Code.Network.HostMigration;
using Code.UI.Popup;
using Code.Utility;
using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using Ricimi;
using UnityEngine.Events;

namespace Code.Network.Lobby
{
    public class LobbyDisconnector : MonoBehaviour
    {
        [SerializeField] private NexusModularPopupOpener disconnectPopup;
        
        private static ServerManager _serverManager;
        private static ClientManager _clientManager;
        private static LobbyController _lobbyController;

        private static NexusModularPopupOpener _disconnectPopup;

        private void Awake()
        {
            _serverManager = InstanceFinder.ServerManager;
            _clientManager = InstanceFinder.ClientManager;
            _lobbyController = _clientManager.GetComponent<LobbyController>();
            
            _disconnectPopup = disconnectPopup;
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public static void Disconnect(bool showPopup = false, string popupTitle = "", string popupMessage = "", UnityAction popupOkAction = null)
        {
            CursorManager.Instance.ShowCursor();
            
            if (showPopup)
                ShowPopup(popupTitle, popupMessage, popupOkAction);
            
            if (_clientManager)
                _clientManager.StopConnection();
            if (_serverManager)
                _serverManager.StopConnection(false);
            if (_lobbyController)
                _lobbyController.LeaveLobby();

        }

        private static void ShowPopup(string popupTitle, string popupMessage, UnityAction popupOkAction)
        {
            popupOkAction ??= DefaultOkAction;
            _disconnectPopup.Title = "You was disconnected from the server";
            _disconnectPopup.Subtitle = popupTitle;
            _disconnectPopup.Message = popupMessage;
            _disconnectPopup.Buttons[0].OnClickedEvent.AddListener(popupOkAction);
            _disconnectPopup.OpenPopup();
        }

        private static void DefaultOkAction()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scenes/Init");
        }
    }
}