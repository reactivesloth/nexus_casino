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
using UnityEngine.UI;

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
            
            var okButton = new ButtonInfo
            {
                Label = "OK",
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            okButton.OnClickedEvent.AddListener(popupOkAction);
            _disconnectPopup.Buttons.Add(okButton);
            
            _disconnectPopup.OpenPopup();
        }

        private static void DefaultOkAction()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Scenes/Init");
        }
    }
}