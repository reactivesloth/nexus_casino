using Code.Chat;
using Code.InteractionSystem;
using Code.UI.Popup;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Admin
{
    public class SlotControlElement : ControlElement
    {
        [SerializeField] private Button resetButton;

        private NexusModularPopupOpener _popupOpener;
        private AdminPanelHandler _adminPanelHandler;
        private SlotMachineInteractable _slot;

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            _adminPanelHandler = FindAnyObjectByType<AdminPanelHandler>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            resetButton.onClick.AddListener(OnResetSlotButtonClick);
        }

        private void OnDisable()
        {
            resetButton.onClick.RemoveListener(OnResetSlotButtonClick);
        }

        public void Init(SlotMachineInteractable slot)
        {
            _slot = slot;

            titleDisplayText.text = $"Slot №{_slot.IDNumber}";
            SearchKey = titleDisplayText.text;
        }

        private void OnResetSlotButtonClick()
        {
            _popupOpener.Title = LocalizationHelper.GetLocalizedString("admin.slots.reset.answer", "number", _slot.IDNumber);//$"Reset Slot №{_slot.IDNumber}";

            var resetButtonInfo = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("admin.slots.reset"),//"Reset",
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };
            resetButtonInfo.OnClickedEvent.AddListener(ResetSlot);
            _popupOpener.Buttons.Add(resetButtonInfo);
            
            _popupOpener.OpenPopup();
        }
        
        private void ResetSlot() => _adminPanelHandler.ResetSlot(_slot.IDNumber);
    }
}