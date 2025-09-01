using System.Linq;
using Code.Network;
using FishNet.Connection;
using UnityEngine;
using TMPro;

namespace Code.InteractionSystem
{
  public class SlotMachineInteractable : Interactable
  {
    [Header("UI Settings")]
    [SerializeField] private Canvas computer3dCanvas;
    [SerializeField] private Canvas contentCanvas;
    [SerializeField] private TextMeshPro idNumberText;

    [Header("Streaming")]
    [SerializeField] private NetworkImageStream networkImageStream;
    public NetworkImageStream NetworkImageStream => networkImageStream;

    public int IDNumber;

    private const string K_IS_USING = "slot.isUsing";

    private bool _isUsingLocal;
    private bool _inited;
    private bool _wasStarted;

    private void EnsureInit()
    {
      if (_inited) return;
      _inited = true;

      if (idNumberText != null) idNumberText.text = IDNumber.ToString();

      if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
      if (contentCanvas) contentCanvas.gameObject.SetActive(false);

      if (networkImageStream == null)
        networkImageStream = GetComponentInChildren<NetworkImageStream>(includeInactive: true);
    }

    private void Awake() => EnsureInit();

    private void OnEnable()
    {
      EnsureInit();
      OnSyncedChanged += HandleSyncedChanged;
      OnForceApply    += HandleForceApply;
    }

    private void OnDisable()
    {
      OnSyncedChanged -= HandleSyncedChanged;
      OnForceApply    -= HandleForceApply;
      _isUsingLocal = false;
    }

    private void Start() => _wasStarted = true;

    public override void OnStartServer()
    {
      base.OnStartServer();
      RegisterBoolSlot(K_IS_USING, false);
      SetBool(K_IS_USING, false);
    }

    public override void OnStartClient()
    {
      base.OnStartClient();
      EnsureInit();
    }

    public override void OnStopNetwork()
    {
      base.OnStopNetwork();
      _isUsingLocal = false;
    }

    public override string InteractionPrompt
      => !_isUsingLocal ? "Use Computer" : "Exit Computer";

    protected internal override void OnInteract(NetworkConnection conn, bool force)
    {
      if (GetBool(K_IS_USING)) return;
      RequestSetBool(K_IS_USING, true, conn);
      ApplyComputerStateImmediate(true);
      if (contentCanvas != null) contentCanvas.gameObject.SetActive(true);
    }

    protected internal override void OnEndInteract(NetworkConnection conn)
    {
      if (GetBool(K_IS_USING))
      {
        RequestSetBool(K_IS_USING, false, conn);
        ApplyComputerStateImmediate(false);
        if (contentCanvas != null) contentCanvas.gameObject.SetActive(false);
      }
    }

    private void HandleSyncedChanged(string key, object prev, object next, bool asServer)
    {
      if (key != K_IS_USING) return;

      bool open = (bool)next;
      ApplyComputerStateImmediate(open);
    }

    private void HandleForceApply()
    {
      bool open = GetBool(K_IS_USING);
      ApplyComputerStateImmediate(open);
    }

    private void ApplyComputerStateImmediate(bool open)
    {
      _isUsingLocal = open;
      if (!_wasStarted) return;

      bool useFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1;

      if (!open)
      {
        if (contentCanvas) contentCanvas.gameObject.SetActive(false);
        if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);

        if (networkImageStream != null)
          networkImageStream.ClearTexture();

        if (IsOwner)
        {
          if (WebViewManager.Instance != null)
          {
            WebViewManager.Instance.HideWorldView(IDNumber);
            WebViewManager.Instance.Hide();
          }

          if (PlayerInput.Instance != null)
          {
            PlayerInput.Instance.HideMobileFallback = false;
            PlayerInput.Instance.IsBusy = false;
          }
        }
        return;
      }

      if (contentCanvas) contentCanvas.gameObject.SetActive(true);

      if (IsOwner)
      {
        if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;

        if (useFS)
        {
          WebViewManager.Instance.OpenFullscreen();
          if (networkImageStream != null && WebViewManager.Instance.WebViewRawImage != null)
            networkImageStream.SetTexture(WebViewManager.Instance.WebViewRawImage);
          if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = true;
        }
        else
        {
          if (computer3dCanvas && !computer3dCanvas.gameObject.activeSelf)
            computer3dCanvas.gameObject.SetActive(true);

          var raw = WebViewManager.Instance.ShowWorldView(IDNumber, computer3dCanvas);
          if (networkImageStream != null)
            networkImageStream.SetTexture(raw);

          if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = false;
        }
      }
      else
      {
        if (computer3dCanvas && !computer3dCanvas.gameObject.activeSelf)
          computer3dCanvas.gameObject.SetActive(true);
      }
    }

    public void SwitchFS()
    {
      var newFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0;
      PlayerPrefs.SetInt("PlayerSlotMachineIsFullscreen", newFS ? 1 : 0);
      PlayerPrefs.Save();
      ApplyComputerStateImmediate(GetBool(K_IS_USING));
    }

    public static SlotMachineInteractable FindById(int id)
    {
      var all = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
      return all.FirstOrDefault(s => s.IDNumber == id);
    }
  }
}