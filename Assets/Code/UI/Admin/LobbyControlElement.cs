using System;
using System.Collections.Generic;
using System.Linq;
using Code.Chat;
using Code.Network.Lobby;
using Code.UI.Popup;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class LobbyControlElement : ControlElement
    {
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text adminsStatusText;
        [SerializeField] private TMP_Text playersCountText;
        [SerializeField] private TMP_Text privateText;

        [SerializeField] private Button moveToButton;

        private string _lobbyId;
        private NexusModularPopupOpener _popupOpener;
        private AdminPanelHandler _adminPanelHandler;
        
        // Кеш для логики исключения дубликатов
        private List<string> _allAvailablePlayers = new List<string>();
        private const string EMPTY_SELECTION = "(не выбрано)";
        
        // Флаг для предотвращения рекурсии
        private bool _isUpdatingDropdowns = false;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            moveToButton.onClick.AddListener(OnMoveToButtonClick);
        }

        private void OnDisable()
        {
            moveToButton.onClick.RemoveListener(OnMoveToButtonClick);
        }

        public void Init(LobbyDetails lobby)
        {
            Network.Lobby.EOSCoroutines.Lobby.GetLobbyInfo(lobby, out var info);
            if (!info.HasValue)
            {
                Destroy(gameObject);
                return;
            }

            var lobbyId = info.Value.LobbyId;
            _lobbyId = lobbyId;

            var nameResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.Name, out var nameAttr);
            var lobbyName = nameResult == Result.Success
                ? nameAttr.Value.Data.Value.Value.AsUtf8.ToString()
                : string.Empty;

            var hostInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.HostIn, out var hostInAttr);
            var hostIn = hostInResult == Result.Success
                ? hostInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var moderInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.ModerIn, out var moderInAttr);
            var moderIn = moderInResult == Result.Success
                ? moderInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var adminInResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.AdminIn, out var adminInAttr);
            var adminIn = adminInResult == Result.Success
                ? adminInAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var privateResult =
                Network.Lobby.EOSCoroutines.Lobby.GetAttribute(lobby, LobbyController.Private, out var privateAttr);
            var isPrivate = privateResult == Result.Success
                ? privateAttr.Value.Data.Value.Value.AsUtf8 == bool.TrueString
                : false;

            var maxPlayersCount = info.Value.MaxMembers;
            var currentPlayersCount = Network.Lobby.EOSCoroutines.Lobby.GetMembers(lobby).Count;

            titleDisplayText.text = lobbyName;
            idText.text = lobbyId;
            playersCountText.text = $"{currentPlayersCount}/{maxPlayersCount}";
            privateText.text = isPrivate ? "Private" : "Public";

            var adminText = adminIn ? "Admin\n" : string.Empty;
            var hostText = hostIn ? "Host\n" : string.Empty;
            var moderText = moderIn ? "Moderator\n" : string.Empty;
            adminsStatusText.text = $"{adminText}{hostText}{moderText}";

            SearchKey = lobbyName;
        }

        private void OnMoveToButtonClick()
        {
            _popupOpener.Title = "Move To Lobby";
            _popupOpener.Subtitle = $"Lobby {_lobbyId}";

            var addPlayerButton = new ButtonInfo
            {
                Label = "Add Player",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            addPlayerButton.OnClickedEvent.AddListener(AddPlayerToPopup);

            var removePlayerButton = new ButtonInfo
            {
                Label = "Remove Player",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            removePlayerButton.OnClickedEvent.AddListener(RemovePlayerFromPopup);

            var movePlayersButton = new ButtonInfo
            {
                Label = "Move Players",
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            movePlayersButton.OnClickedEvent.AddListener(OnMoveClicked);

            _popupOpener.Buttons.Add(addPlayerButton);
            _popupOpener.Buttons.Add(removePlayerButton);
            _popupOpener.Buttons.Add(movePlayersButton);

            _popupOpener.OpenPopup();

            // Инициализация логики исключения дубликатов
            InitializePlayerSelectionSystem();
        }

        private void InitializePlayerSelectionSystem()
        {
            // Кешируем всех доступных игроков
            _allAvailablePlayers = LobbyVariables.Instance.currentLobby.lobbyMembers
                .Select(m => m.displayName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .ToList();

            // Добавляем первый дропдаун
            AddPlayerToPopup();
        }

        private void AddPlayerToPopup()
        {
            var popup = _popupOpener.LastPopup;
            if (popup == null) return;

            // Создаем список опций с пустой опцией в начале
            var initialOptions = new List<string> { EMPTY_SELECTION };
            initialOptions.AddRange(_allAvailablePlayers);

            // Добавляем новый дропдаун
            popup.AddDropdown("Player", initialOptions.ToArray());
            
            // Получаем индекс последнего добавленного дропдауна
            var lastIndex = popup.Inputs.Count - 1;
            
            // Подписываемся на изменения в новом дропдауне
            if (popup.Inputs[lastIndex] is TMP_Dropdown dropdown)
            {
                dropdown.onValueChanged.AddListener(_ => OnDropdownValueChanged());
            }

            // Обновляем все дропдауны с учетом ограничений
            UpdateAllDropdownsWithConstraints();
        }

        private void RemovePlayerFromPopup()
        {
            var popup = _popupOpener.LastPopup;
            if (popup == null || popup.Inputs.Count == 0) return;

            var lastIndex = popup.Inputs.Count - 1;
            
            // Отписываемся от событий перед удалением
            if (popup.Inputs[lastIndex] is TMP_Dropdown dropdown)
            {
                dropdown.onValueChanged.RemoveAllListeners();
            }

            popup.RemoveInputAt(lastIndex);
            
            // Обновляем оставшиеся дропдауны
            UpdateAllDropdownsWithConstraints();
        }

        private void OnDropdownValueChanged()
        {
            // Предотвращаем рекурсию
            if (_isUpdatingDropdowns)
                return;

            UpdateAllDropdownsWithConstraints();
        }

        private void UpdateAllDropdownsWithConstraints()
        {
            var popup = _popupOpener.LastPopup;
            if (popup == null || _isUpdatingDropdowns) return;

            _isUpdatingDropdowns = true;
            
            try
            {
                // Собираем текущие выбранные значения (исключая пустые)
                var selectedPlayers = GetCurrentSelectedPlayers();

                // Обновляем каждый дропдаун
                for (int i = 0; i < popup.Inputs.Count; i++)
                {
                    if (popup.Inputs[i] is TMP_Dropdown dropdown)
                    {
                        UpdateDropdownOptions(i, dropdown, selectedPlayers);
                    }
                }
            }
            finally
            {
                _isUpdatingDropdowns = false;
            }
        }

        private HashSet<string> GetCurrentSelectedPlayers()
        {
            var popup = _popupOpener.LastPopup;
            var selectedPlayers = new HashSet<string>();
            
            if (popup == null) return selectedPlayers;

            for (int i = 0; i < popup.Inputs.Count; i++)
            {
                if (popup.Inputs[i] is TMP_Dropdown dropdown && dropdown.options.Count > 0)
                {
                    var selectedValue = dropdown.options[dropdown.value].text;
                    if (!string.IsNullOrEmpty(selectedValue) && selectedValue != EMPTY_SELECTION)
                    {
                        selectedPlayers.Add(selectedValue);
                    }
                }
            }

            return selectedPlayers;
        }

        private void UpdateDropdownOptions(int dropdownIndex, TMP_Dropdown targetDropdown, HashSet<string> allSelectedPlayers)
        {
            var popup = _popupOpener.LastPopup;
            if (popup == null) return;

            // Получаем текущее значение этого дропдауна
            var currentValue = EMPTY_SELECTION;
            if (targetDropdown.options.Count > 0)
            {
                currentValue = targetDropdown.options[targetDropdown.value].text;
            }

            // Создаем список доступных игроков: все игроки минус уже выбранные в других дропдаунах
            var availablePlayers = _allAvailablePlayers
                .Where(player => !allSelectedPlayers.Contains(player) || player == currentValue)
                .ToList();

            // Формируем финальный список опций: всегда начинаем с пустой опции
            var finalOptions = new List<string> { EMPTY_SELECTION };
            finalOptions.AddRange(availablePlayers);

            // Обновляем опции дропдауна
            popup.SetDropdownOptions(dropdownIndex, finalOptions, currentValue);
        }

        private void OnMoveClicked()
        {
            var popup = _popupOpener.LastPopup;
            if (popup == null) return;

            // Собираем всех выбранных игроков (исключая пустые значения)
            var selectedPlayers = new List<string>();
            
            for (var i = 0; i < popup.Inputs.Count; i++)
            {
                var playerName = popup.GetInputValue(i);
                if (!string.IsNullOrEmpty(playerName) && playerName != EMPTY_SELECTION)
                {
                    selectedPlayers.Add(playerName);
                }
            }

            // Удаляем дубликаты на всякий случай
            var uniquePlayers = selectedPlayers.Distinct().ToList();

            // Перемещаем каждого уникального игрока
            foreach (var playerName in uniquePlayers)
            {
                _adminPanelHandler.MoveUserToRoom(playerName, _lobbyId);
            }

            _popupOpener.ClosePopup();
        }
    }
}