using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonOpenURL : MonoBehaviour
{
    [SerializeField] [NotNull] private string url;
    private Button m_button;

    private void Awake()
    {
        m_button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        m_button.onClick.AddListener(OnButtonClick);
    }

    private void OnDisable()
    {
        m_button.onClick.RemoveListener(OnButtonClick);
    }
    
    private void OnButtonClick()
    {
        Application.OpenURL(url);
    }
}
