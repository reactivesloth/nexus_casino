using System;
using System.Collections;
using System.Collections.Generic;
using Code.Chat;
using Code.Network.Lobby;
using Code.Scene.SceneObjectControl;
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
        [SerializeField] private Button refreshButton;

        [Header("Tabs")] [SerializeField] private Button usersButton;

        [SerializeField] private Button sceneButton;

        //[SerializeField] private Button slotsButton;
        [SerializeField] private Button lobbiesButton;

        [Header("Prefabs")] [SerializeField] private UserControlElement userControlElementPrefab;

        [SerializeField] private SceneControlElement sceneControlElementPrefab;

        //[SerializeField] private SlotControlElement slotControlElementPrefab;
        [SerializeField] private LobbyControlElement lobbyControlElementPrefab;

        private ControlElement _currentControlPrefab;
        private readonly List<ControlElement> _controlElements = new();

        private AdminPanelHandler _adminPanelHandler;

        private void Awake()
        {
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            searchField.onValueChanged.AddListener(OnSearchInputChange);

            refreshButton.onClick.AddListener(OnRefreshButtonClick);

            usersButton.onClick.AddListener(OnUsersButtonClick);
            sceneButton.onClick.AddListener(OnSceneButtonClick);
            //slotsButton.onClick.AddListener(OnSlotsButtonClick);
            lobbiesButton.onClick.AddListener(OnLobbiesButtonClick);
        }

        private void OnDisable()
        {
            searchField.onValueChanged.RemoveListener(OnSearchInputChange);

            refreshButton.onClick.RemoveListener(OnRefreshButtonClick);

            usersButton.onClick.RemoveListener(OnUsersButtonClick);
            sceneButton.onClick.RemoveListener(OnSceneButtonClick);
            //slotsButton.onClick.RemoveListener(OnSlotsButtonClick);
            lobbiesButton.onClick.RemoveListener(OnLobbiesButtonClick);
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
                /*case SlotControlElement:
                    UpdateSlots();
                    break;*/
                case LobbyControlElement:
                    UpdateLobbies();
                    break;
                default:
                    return;
            }
        }

        private void OnUsersButtonClick()
        {
            OnStartNewTab();

            UpdateUsers();
        }

        private void OnSceneButtonClick()
        {
            OnStartNewTab();

            UpdateScene();
        }

        /*private void OnSlotsButtonClick()
        {
            OnStartNewTab();

            UpdateSlots();
        }*/

        private void OnLobbiesButtonClick()
        {
            OnStartNewTab();

            UpdateLobbies();
        }

        #endregion

        private void OnStartNewTab()
        {
            searchField.text = string.Empty;
        }

        private void UpdateUsers()
        {
            ClearContent();
            // состояние для отображения: имя, роль, муты
            var lobbyMembers = LobbyVariables.Instance.currentLobby.lobbyMembers;
            foreach (var lobbyMember in lobbyMembers)
            {
                var controlElement = Instantiate(userControlElementPrefab, contentContainer);
                controlElement.Init(lobbyMember);
                _controlElements.Add(controlElement);
            }
        }

        private void UpdateScene()
        {
            ClearContent();
            var sceneObjects = SceneObjectController.AllSceneObjects;
            foreach (var sceneObject in sceneObjects)
            {
                var controlElement = Instantiate(sceneControlElementPrefab, contentContainer);
                controlElement.Init(sceneObject);
                _controlElements.Add(controlElement);
            }
        }

        /*private void UpdateSlots()
        {

        }*/

        private void UpdateLobbies()
        {
            ClearContent();
            StartCoroutine(UpdateLobbiesListRoutine());
        }

        private IEnumerator UpdateLobbiesListRoutine()
        {
            var lobbyController = FindAnyObjectByType<LobbyController>();
            yield return StartCoroutine(lobbyController.PollLobbiesRoutine());
            var lobbies = lobbyController.GetAllLobbies();
            foreach (var lobby in lobbies)
            {
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
    }
}