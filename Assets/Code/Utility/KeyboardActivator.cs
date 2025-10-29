using UnityEngine;

public class KeyboardActivator : MonoBehaviour
{
    public GameObject keyboardOpener;
    
    private void Update()
    {
        if (keyboardOpener.activeSelf != TouchScreenKeyboard.visible)
        {
            keyboardOpener.SetActive(TouchScreenKeyboard.visible);
        }
    }
}