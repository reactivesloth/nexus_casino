using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class KeyboardManager : MonoBehaviour
{
    
    public static KeyboardManager Instance;
    public GameObject KeyboardObject;
    private TMP_InputField _currentInputField;

    private void Start()
    {
        Instance = this;
        Close();
    }

    public void DeleteLetter()
    {
        if (_currentInputField == null) return;
        
        if(_currentInputField.text.Length != 0)
            _currentInputField.text = _currentInputField.text.Remove(_currentInputField.text.Length - 1, 1);
    }

    public void AddLetter(string letter)
    {
        if (_currentInputField == null) return;

        _currentInputField.text += letter;
    }

    public void SubmitWord()
    {
        if (_currentInputField == null) return;
        Close();
    }

    public void Close()
    {
        Vector2 pos = Vector2.zero;
        if (Input.touches.Length > 0)
            pos = Input.touches.Last().position;
        else
            pos = Input.mousePosition;
    
        Vector2 localMousePosition = _currentInputField.GetComponent<RectTransform>().InverseTransformPoint(pos);
        Vector2 localMousePosition2 = KeyboardObject.GetComponent<RectTransform>().InverseTransformPoint(pos);
        if (!_currentInputField.GetComponent<RectTransform>().rect.Contains(localMousePosition) && !KeyboardObject.GetComponent<RectTransform>().rect.Contains(localMousePosition2))
        {
            KeyboardObject.SetActive(false);
        }
    }

    public void Show(TMP_InputField inputField, KeyboardType type = KeyboardType.Full)
    {
        KeyboardController controller = KeyboardObject.GetComponentInChildren<KeyboardController>();
        controller.SetType(type);
        
        KeyboardObject.SetActive(true);
        _currentInputField = inputField;
        _currentInputField.MoveTextEnd(false);
    }
}
