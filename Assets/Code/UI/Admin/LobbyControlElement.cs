using System.Collections.Generic;
using System.Linq;
using Code.Network;
using Code.UI.Popup;
using Code.Utility;
using Newtonsoft.Json.Linq;
using PurrNet;
using PurrNet.Transports;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PlayerSpawner = Code.Network.Player.PlayerSpawner;

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
        private const string EMPTY_SELECTION = "-";

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

        public void Init(EdgegapAdminAPI.ServerInstanceItem serverData)
        {
            _lobbyId = serverData.request_id;

            string currentId = PlayerPrefs.GetString("Current_Server_RequestId", "");
            bool isCurrent = !string.IsNullOrEmpty(currentId) && serverData.request_id == currentId;

            titleDisplayText.text = (isCurrent ? "▶ " : "") + (serverData.metadata?.name ?? serverData.request_id);
            idText.text = serverData.request_id;
            adminsStatusText.text = serverData.server?.location != null
                ? $"{serverData.server.location.city}, {serverData.server.location.country}"
                : "";

            playersCountText.text = $"{serverData.total_reserved_seats}/{serverData.metadata?.max_players ?? 0}";

            var isPrivate = serverData.total_joinable_seats == 0;
            var privateKey = isPrivate ? "admin.lobby.private.close" : "admin.lobby.private.open";
            LocalizationHelper.SetLocalizedTextAsync(privateText, privateKey);
        }

        private void OnMoveToButtonClick()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.lobbies.moveto.title");

            var addPlayerButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.lobbies.moveto.add_player"),
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            addPlayerButton.OnClickedEvent.AddListener(AddPlayerToPopup);

            var removePlayerButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.lobbies.moveto.remove_player"),
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            removePlayerButton.OnClickedEvent.AddListener(RemovePlayerFromPopup);

            var movePlayersButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.lobbies.moveto.move"),
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
            _allAvailablePlayers = PlayerSpawner.NameConnectionsData.Keys.Distinct().ToList();

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

        private void UpdateDropdownOptions(int dropdownIndex, TMP_Dropdown targetDropdown,
            HashSet<string> allSelectedPlayers)
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