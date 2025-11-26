using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Chat;
using Code.InteractionSystem;
using Code.Scene.SceneObjectControl;
using Code.UI.Popup;
using Code.Utility;
using PlayFlow;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class AdminControlPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Header("Common")] [SerializeField] private Transform contentContainer;
        [SerializeField] private TMP_InputField searchField;

        [Header("Buttons")] [SerializeField] private Button newLobbyButton;
        [SerializeField] private Button closePanelButton;
        [SerializeField] private Button refreshButton;

        [Header("Tabs")] [SerializeField] private Button usersButton;
        [SerializeField] private Button sceneButton;
        [SerializeField] private Button slotsButton;
        [SerializeField] private Button lobbiesButton;

        [Header("Prefabs")] [SerializeField] private UserControlElement userControlElementPrefab;

        [SerializeField] private SceneControlElement sceneControlElementPrefab;

        [SerializeField] private SlotControlElement slotControlElementPrefab;
        [SerializeField] private LobbyControlElement lobbyControlElementPrefab;

        private ControlElement _currentControlPrefab;
        private readonly List<ControlElement> _controlElements = new();

        private AdminPanelHandler _adminPanelHandler;
        private NexusModularPopupOpener _popupOpener;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            searchField.onValueChanged.AddListener(OnSearchInputChange);

            refreshButton.onClick.AddListener(OnRefreshButtonClick);
            newLobbyButton.onClick.AddListener(OnNewLobbyButtonClick);
            closePanelButton.onClick.AddListener(OnClosePanelButtonClick);

            usersButton.onClick.AddListener(OnUsersButtonClick);
            sceneButton.onClick.AddListener(OnSceneButtonClick);
            slotsButton.onClick.AddListener(OnSlotsButtonClick);
            lobbiesButton.onClick.AddListener(OnLobbiesButtonClick);
        }

        private void Update()
        {
            if (PlayerInput.Instance.OpenAdminPanelDown)
                SetActive(!panel.activeSelf);
            // Закрытие чата по Escape
            if (PlayerInput.Instance.IsPausedDown && panel.activeSelf)
                SetActive(false);
        }

        private void OnDisable()
        {
            searchField.onValueChanged.RemoveListener(OnSearchInputChange);

            refreshButton.onClick.RemoveListener(OnRefreshButtonClick);
            newLobbyButton.onClick.RemoveListener(OnNewLobbyButtonClick);
            closePanelButton.onClick.RemoveListener(OnClosePanelButtonClick);

            usersButton.onClick.RemoveListener(OnUsersButtonClick);
            sceneButton.onClick.RemoveListener(OnSceneButtonClick);
            slotsButton.onClick.RemoveListener(OnSlotsButtonClick);
            lobbiesButton.onClick.RemoveListener(OnLobbiesButtonClick);
        }

        public void SetActive(bool value)
        {
            if (!value)
            {
                CursorManager.Instance.HideCursor();
            }
            else
            {
                CursorManager.Instance.ShowCursor();
            }

            panel.SetActive(value);
            if (panel.activeSelf)
                OnUsersButtonClick();

            PlayerInput.Instance.IsBusy = value;
        }

        #region UI Callbacks

        private void OnSearchInputChange(string newValue) => _controlElements.ForEach(element =>
            element.gameObject.SetActive(element.SearchKey.Contains(newValue)));

        private void OnRefreshButtonClick()
        {
            switch (_currentControlPrefab)
            {
                case UserControlElement:
                    UpdateUsers();
                    break;
                case SceneControlElement:
                    UpdateScene();
                    break;
                case SlotControlElement:
                    UpdateSlots();
                    break;
                case LobbyControlElement:
                    UpdateLobbies();
                    break;
                default:
                    return;
            }
        }

        private void OnUsersButtonClick()
        {
            _currentControlPrefab = userControlElementPrefab;
            OnStartNewTab();
            SetTabsOutline(usersButton);
            SetTabsInteractable(usersButton);
            UpdateUsers();
        }

        private void OnSceneButtonClick()
        {
            _currentControlPrefab = sceneControlElementPrefab;
            OnStartNewTab();
            SetTabsOutline(sceneButton);
            SetTabsInteractable(sceneButton);
            UpdateScene();
        }

        private void OnSlotsButtonClick()
        {
            _currentControlPrefab = slotControlElementPrefab;
            OnStartNewTab();
            SetTabsOutline(slotsButton);
            SetTabsInteractable(slotsButton);
            UpdateSlots();
        }

        private void OnLobbiesButtonClick()
        {
            _currentControlPrefab = lobbyControlElementPrefab;
            OnStartNewTab();
            SetTabsOutline(lobbiesButton);
            SetTabsInteractable(lobbiesButton);
            UpdateLobbies();
        }

        private void SetTabsOutline(Button currentTabButton)
        {
            usersButton.GetComponent<Outline>().enabled = usersButton == currentTabButton;
            sceneButton.GetComponent<Outline>().enabled = sceneButton == currentTabButton;
            lobbiesButton.GetComponent<Outline>().enabled = lobbiesButton == currentTabButton;
            slotsButton.GetComponent<Outline>().enabled = slotsButton == currentTabButton;
        }

        private void SetTabsInteractable(Button currentTabButton)
        {
            usersButton.interactable = usersButton != currentTabButton;
            sceneButton.interactable = sceneButton != currentTabButton;
            lobbiesButton.interactable = lobbiesButton != currentTabButton;
            slotsButton.interactable = slotsButton != currentTabButton;
        }

        #endregion

        private void OnStartNewTab()
        {
            searchField.text = string.Empty;
        }

        private void UpdateUsers()
        {
            ClearContent();

            var lobbyIds = PlayFlowLobbyManagerV2.Instance.CurrentLobby.players;

            foreach (var playerId in lobbyIds)
            {
                if(!PlayFlowLobbyManagerV2.Instance.CurrentLobby.lobbyStateRealTime.TryGetValue(playerId, out var playerData))
                    continue;
                
                var controlElement = Instantiate(userControlElementPrefab, contentContainer);
                controlElement.Init(playerId, playerData);
                _controlElements.Add(controlElement);
            }
        }

        private void UpdateScene()
        {
            ClearContent();
            var sceneObjects = SceneObjectsController.AllSceneObjects;
            foreach (var sceneObject in sceneObjects)
            {
                var controlElement = Instantiate(sceneControlElementPrefab, contentContainer);
                controlElement.Init(sceneObject);
                _controlElements.Add(controlElement);
            }
        }

        private void UpdateSlots()
        {
            ClearContent();
            var slots = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).OrderBy(s => s.IDNumber);
            foreach (var slot in slots)
            {
                var controlElement = Instantiate(slotControlElementPrefab, contentContainer);
                controlElement.Init(slot);
                _controlElements.Add(controlElement);
            }
        }

        private void UpdateLobbies()
        {
            ClearContent();
            PlayFlowLobbyManagerV2.Instance.GetAvailableLobbies(OnLobbiesReceived, Debug.LogError);
        }

        private void OnLobbiesReceived(List<Lobby> lobbies)
        {
            foreach (var lobby in lobbies)
            {
                if(lobby.currentPlayers <= 0)
                    continue;
                
                var controlElement = Instantiate(lobbyControlElementPrefab, contentContainer);
                controlElement.Init(lobby);
                _controlElements.Add(controlElement);
            }
        }

        private void ClearContent()
        {
            _controlElements.ForEach(e => Destroy(e.gameObject));
            _controlElements.Clear();
        }

        private void OnNewLobbyButtonClick()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.new_lobby"); // "New Lobby";
            _popupOpener.Subtitle =
                LocalizationHelper
                    .GetLocalizedString("admin.new_lobby.sub"); // $"Choise lobby name, privateStatus and host";

            var createButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.new_lobby.create"), //"Add Player",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            createButton.OnClickedEvent.AddListener(CreateLobbyClicked);

            _popupOpener.Inputs.Add(new InputInfo
            {
                labelName = LocalizationHelper.GetLocalizedString("admin.new_lobby.name"), //"Lobby Name",
                type = InputInfoType.InputField,
                contentType = TMP_InputField.ContentType.Standard
            });

            _popupOpener.Inputs.Add(new InputInfo
            {
                labelName = LocalizationHelper.GetLocalizedString("admin.lobby.private"),
                type = InputInfoType.Dropdown,
                valueVariants = new List<string>
                {
                    LocalizationHelper.GetLocalizedString("admin.lobby.private.open"),
                    LocalizationHelper.GetLocalizedString("admin.lobby.private.close")
                }
            });

            
            
            var hostVariants = new List<string> { "me" };
            var allAvailablePlayers = PlayFlowLobbyManagerV2.Instance.CurrentLobby.players.ToList()
                .Select(id =>
                {
                    if (PlayFlowLobbyManagerV2.Instance.CurrentLobby.lobbyStateRealTime.TryGetValue(id, out var playerData)
                        && playerData.TryGetValue("name", out var playerName))
                        return playerName.ToString();
                    return string.Empty;
                })
                .Where(playerName => !string.IsNullOrEmpty(playerName))
                .Distinct()
                .ToList();
            
            hostVariants.AddRange(allAvailablePlayers);

            _popupOpener.Inputs.Add(new InputInfo
            {
                labelName = LocalizationHelper.GetLocalizedString("admin.lobby.host"),
                type = InputInfoType.Dropdown,
                valueVariants = hostVariants
            });

            _popupOpener.Buttons.Add(createButton);

            _popupOpener.OpenPopup();
        }

        private void OnClosePanelButtonClick()
        {
            SetActive(false);
        }

        private void CreateLobbyClicked()
        {
            var roomName = _popupOpener.LastPopup.GetInputValue(0);
            var isPrivate = _popupOpener.LastPopup.GetCurrentDropdownElementIndex(1) != 0;
            var hostName = _popupOpener.LastPopup.GetCurrentDropdownElementIndex(2) == 0
                ? null
                : _popupOpener.LastPopup.GetInputValue(2);

            _adminPanelHandler.NewRoomHandle(roomName, isPrivate, hostName);

            _popupOpener.ClosePopup();
        }
    }
}