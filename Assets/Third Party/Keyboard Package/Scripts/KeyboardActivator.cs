using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class KeyboardActivator : MonoBehaviour
{
    public void OpenKeyboardFull(TMP_InputField field)
    {
        KeyboardManager.Instance.Show(field, KeyboardType.Full);
    }

    public void OpenKeyboardOnlyNumbers(TMP_InputField field)
    {
        KeyboardManager.Instance.Show(field, KeyboardType.OnlyNumbers);
    }

    public void OpenKeyboardOnlyLetters(TMP_InputField field)
    {
        KeyboardManager.Instance.Show(field, KeyboardType.OnlyLetters);
    }

    public void CloseKeyboard()
    {
        KeyboardManager.Instance.Close();
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CloseKeyboard();
        }
    }
}