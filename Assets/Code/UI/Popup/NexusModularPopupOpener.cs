// Copyright (C) 2023 ricimi. All rights reserved.
// This code can only be used under the standard Unity Asset Store EULA,
// a copy of which is available at https://unity.com/legal/as-terms.

using System;
using System.Collections.Generic;
using Ricimi;
using TMPro;
using UnityEngine;

namespace Code.UI.Popup
{
    // Utility component to open a modular popup. See the associated ModularPopup script.
    public class NexusModularPopupOpener : PopupOpener
    {
        [Header("Text")] public string Title;
        public string Subtitle;
        [TextArea(minLines: 3, maxLines: 3)] public string Message;

        [Space] [Header("Image")] public Sprite Image;
        public Color32 TintColor = Color.white;
        public string Caption;

        [Space] [Header("Buttons")] public List<ButtonInfo> Buttons;
        public List<InputInfo> Inputs;
        public Action ManualyCloseAction;
        
        public NexusModularPopup LastPopup => m_popup.GetComponent<NexusModularPopup>();
        public bool Opened;
        
        public override void OpenPopup()
        {
            base.OpenPopup();
            Opened = true;
            m_popup.GetComponent<NexusModularPopup>().Initialize(this);
            ResetValues();
        }

        public override void ClosePopup()
        {
            base.ClosePopup();
            Opened = false;
            m_popup.GetComponent<NexusModularPopup>().Close();
        }

        private void ResetValues()
        {
            ManualyCloseAction = null;
            Title = string.Empty;
            Subtitle = string.Empty;
            Message = string.Empty;
            Image = null;
            TintColor = Color.white;
            Caption = string.Empty;
            Buttons.Clear();
            Inputs.Clear();
        }
    }

    [System.Serializable]
    public class InputInfo
    {
        public InputInfoType type;
        public string labelName;
        public TMP_InputField.ContentType contentType;
        public List<string> valueVariants;
    }

    public enum InputInfoType
    {
        InputField,
        Dropdown
    }
}