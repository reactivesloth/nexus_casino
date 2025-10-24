using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class KeyboardActivator : MonoBehaviour
{
    public void OpenKeyboard(TMP_InputField field)
    {
        if (!Input.touchSupported)
            return;
        
        KeyboardManager.Instance.Show(field);
    }

    public void CloseKeyboard()
    {
        KeyboardManager.Instance.Close();
    }

    public void Update()
    {
        if (!Input.touchSupported)
            return;
        
        if (Input.GetMouseButtonDown(0))
        {
            CloseKeyboard();
        }
    }
}
