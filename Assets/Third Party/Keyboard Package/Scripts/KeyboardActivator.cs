using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class KeyboardActivator : MonoBehaviour
{
    public void OpenKeyboardFull(TMP_InputField field)
    {
        if (!Input.touchSupported) return;
            KeyboardManager.Instance.Show(field, KeyboardType.Full);
    }

    public void OpenKeyboardOnlyNumbers(TMP_InputField field)
    {
        if (!Input.touchSupported) return;
            KeyboardManager.Instance.Show(field, KeyboardType.OnlyNumbers);
    }

    public void OpenKeyboardOnlyLetters(TMP_InputField field)
    {
        if (!Input.touchSupported) return;
            KeyboardManager.Instance.Show(field, KeyboardType.OnlyLetters);
    }

    public void CloseKeyboard()
    {
        KeyboardManager.Instance.Close();
    }

    public void Update()
    {
        if (!Input.touchSupported) return;
        if (Input.GetMouseButtonDown(0) || Input.touches.Length > 0)
        {
            CloseKeyboard();
        }
    }
}