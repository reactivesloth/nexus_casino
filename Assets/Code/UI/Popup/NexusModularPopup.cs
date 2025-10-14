// Copyright (C) 2023 ricimi. All rights reserved.
// This code can only be used under the standard Unity Asset Store EULA,
// a copy of which is available at [https://unity.com/legal/as-terms](https://unity.com/legal/as-terms).

using System;
using System.Collections.Generic;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Code.UI.Popup
{
    // This UI component represents a modular popup that can be used to easily represent
    // many different types of popups.
    public class NexusModularPopup : Ricimi.Popup
    {
        [SerializeField] private Button buttonClose;
        [Header("Text")] public TextMeshProUGUI Title;
        public TextMeshProUGUI Subtitle;
        public TextMeshProUGUI Message;

        [Space] [Header("Image")] public Image Image;
        public TextMeshProUGUI Caption;

        [Space] [Header("Buttons")] public GameObject ButtonGroup;
        public List<Button> Buttons;

        [Space] [Header("Inputs")] [SerializeField]
        private TMP_InputField inputFieldPrefab;

        [SerializeField] private TMP_Dropdown dropdownPrefab;

        public GameObject InputGroup;
        public List<Selectable> Inputs;
        
        public void Initialize(NexusModularPopupOpener opener)
        {
            buttonClose.onClick.AddListener(Close);

            SetLabel(Title, opener.Title);
            SetLabel(Subtitle, opener.Subtitle);
            SetLabel(Message, opener.Message);

            SetImage(Image, opener.Image, opener.TintColor);
            SetLabel(Caption, opener.Caption);

            foreach (var button in Buttons)
            {
                button.gameObject.SetActive(false);
            }

            if (opener.Buttons.Count == 0)
            {
                ButtonGroup.SetActive(false);
            }
            else
            {
                for (var i = 0; i < opener.Buttons.Count; i++)
                {
                    SetButton(Buttons[i], opener.Buttons[i]);
                }
            }

            if (opener.Inputs.Count == 0)
            {
                InputGroup.SetActive(false);
            }

            foreach (var openerInput in opener.Inputs)
            {
                switch (openerInput.type)
                {
                    case InputInfoType.InputField:
                        AddInputField(openerInput.labelName, openerInput.contentType);
                        break;
                    case InputInfoType.Dropdown:
                        AddDropdown(openerInput.labelName, openerInput.valueVariants.ToArray());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void AddInputField(string label, TMP_InputField.ContentType contentType)
        {
            var newInputField = Instantiate(inputFieldPrefab, InputGroup.transform);
            var textPlaceHolder = newInputField.placeholder as TMP_Text;
            if (textPlaceHolder != null)
                textPlaceHolder.text = label;
            newInputField.contentType = contentType;

            Inputs.Add(newInputField);
            
            InputsContainerChange();
        }

        public void AddDropdown(string label, params string[] options)
        {
            var newDropdown = Instantiate(dropdownPrefab, InputGroup.transform);
            var textPlaceHolder = newDropdown.placeholder as TMP_Text;
            if (textPlaceHolder != null)
                textPlaceHolder.text = label;

            newDropdown.options = new List<TMP_Dropdown.OptionData>();
            foreach (var option in options)
                newDropdown.options.Add(new TMP_Dropdown.OptionData(option));

            Inputs.Add(newDropdown);
            
            InputsContainerChange();
        }

        public void UpdateDropdown(int index, List<string> newOptions)
        {
            var selectable = Inputs[index];
            if (selectable is not TMP_Dropdown dropdown)
                return;
            
            dropdown.ClearOptions();
            dropdown.AddOptions(newOptions);
        }

        // Безопасное обновление опций дропдауна без вызова событий
        public void SetDropdownOptions(int index, List<string> newOptions, string preferredValue = null)
        {
            if (index < 0 || index >= Inputs.Count)
                return;
            
            var selectable = Inputs[index];
            if (selectable is not TMP_Dropdown dropdown)
                return;

            // Сохраняем текущее значение, если preferredValue не указано
            var currentValue = preferredValue;
            if (string.IsNullOrEmpty(currentValue) && dropdown.options.Count > 0)
            {
                currentValue = dropdown.options[dropdown.value].text;
            }

            // Сохраняем текущие обработчики событий
            var tempHandlers = new UnityEvent<int>();
            for (int i = 0; i < dropdown.onValueChanged.GetPersistentEventCount(); i++)
            {
                // Копируем обработчики
            }
            var originalEvent = dropdown.onValueChanged;

            // Временно отключаем обработчики событий
            dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();

            try
            {
                // Обновляем опции
                dropdown.ClearOptions();
                dropdown.AddOptions(newOptions);

                // Пытаемся восстановить выбранное значение
                if (!string.IsNullOrEmpty(currentValue))
                {
                    var foundIndex = newOptions.FindIndex(option => option == currentValue);
                    if (foundIndex >= 0)
                    {
                        dropdown.value = foundIndex;
                    }
                    else
                    {
                        dropdown.value = 0; // Если значение не найдено, выбираем первое
                    }
                }
                else
                {
                    dropdown.value = 0;
                }

                dropdown.RefreshShownValue();
            }
            finally
            {
                // Восстанавливаем обработчики событий
                dropdown.onValueChanged = originalEvent;
            }
        }

        public void RemoveInputAt(int index)
        {
            if(index >= Inputs.Count || index < 0)
                return;
            
            var input = Inputs[index];
            Destroy(input.gameObject);
            Inputs.Remove(input);
            InputsContainerChange();
        }

        public string GetInputValue(int index)
        {
            if (index < 0 || index >= Inputs.Count)
                return null;
            var input = Inputs[index];
            switch (input)
            {
                case TMP_InputField inputField:
                    return inputField.text;
                case TMP_Dropdown dropdown:
                    return dropdown.options.Count > 0 ? dropdown.options[dropdown.value].text : null;
                default:
                    return null;
            }
        }

        private void InputsContainerChange()
        {
            InputGroup.gameObject.SetActive(Inputs.Count > 0);
        }

        private void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(text))
            {
                label.text = text;
            }
            else
            {
                label.gameObject.SetActive(false);
            }
        }

        private void SetImage(Image image, Sprite sprite, Color32 color)
        {
            if (image == null)
            {
                return;
            }

            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = color;
            }
            else
            {
                image.gameObject.SetActive(false);
            }
        }

        private void SetButton(Button button, ButtonInfo info)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = info.Label;
            }

            if (!info.IgnoreButtonClickedEvent)
            {
                button.onClick = info.OnClickedEvent;
            }

            if (info.ClosePopupWhenClicked)
            {
                button.onClick.AddListener(Close);
            }
        }
    }
}