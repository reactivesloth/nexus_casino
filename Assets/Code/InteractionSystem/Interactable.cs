using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;

namespace Code.InteractionSystem
{
  public interface IHostMigratable
  {
    byte[] CaptureHostState();
    void RestoreHostState(byte[] data);
  }

  public abstract class Interactable : NetworkBehaviour, IHostMigratable
  {
    [Header("Base settings")] [SerializeField]
    private float _interactionDistance = 3f;

    [SerializeField] private bool _interactableEnabled = true;
    [SerializeField] private bool _manualRelease = false;

    public GameObject[] outlineGameObjects;

    private readonly SyncVar<bool> _occupied = new(new SyncTypeSettings
    {
      WritePermission = WritePermission.ServerOnly,
      ReadPermission = ReadPermission.Observers
    });

    private readonly Dictionary<string, object> _boolSlots = new();
    private readonly Dictionary<string, object> _intSlots = new();
    private readonly Dictionary<string, object> _floatSlots = new();
    private readonly Dictionary<string, object> _stringSlots = new();

    public event Action<string, object, object, bool> OnSyncedChanged;
    public event Action OnForceApply;

    public bool IsEnabled => _interactableEnabled;
    public bool ManualRelease => _manualRelease;
    public bool IsOccupied => _occupied.Value;
    public bool IsBusy { get; set; }

    public virtual string InteractionPrompt
    {
      get
      {
        if (!_interactableEnabled) return "Disabled";
        if (!IsOccupied) return "Press E to interact";
        return _manualRelease ? "Press E to end" : "Occupied";
      }
    }

    private int _occupiedConnectionId = -1;
    private const int MIGRATION_VERSION = 1;

    public override void OnStartServer()
    {
      base.OnStartServer();
      ServerManager.OnRemoteConnectionState += ServerManagerOnRemoteConnectionState;
      _occupied.OnChange += (prev, next, asServer) =>
        OnSyncedChanged?.Invoke("occupied", prev, next, asServer);
      EnsureCoreSlotsRegistered();
    }

    public override void OnStopServer()
    {
      base.OnStopServer();
      ServerManager.OnRemoteConnectionState -= ServerManagerOnRemoteConnectionState;
    }

    public override void OnStartClient()
    {
      base.OnStartClient();
      EnsureCoreSlotsRegistered();
      ForceApplyAll();
    }

    private void EnsureCoreSlotsRegistered()
    {
    }

    [Server]
    private void ServerManagerOnRemoteConnectionState(NetworkConnection connection,
      RemoteConnectionStateArgs stateArgs)
    {
      if (stateArgs.ConnectionState == RemoteConnectionState.Stopped &&
          stateArgs.ConnectionId == _occupiedConnectionId)
        ReleaseInteractable();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
      base.OnValidate();
      var col = GetComponent<Collider>();
      if (col != null) col.isTrigger = true;
    }
#endif

    public void RequestInteract()
    {
      if (!_interactableEnabled || _occupied.Value)
      {
        InteractCallback?.Invoke(false);
        return;
      }

      Server_HandleInteract(ClientManager.Connection);
    }

    public void RequestEndInteract()
    {
      if (!_interactableEnabled || !_occupied.Value || !_manualRelease)
      {
        InteractCallback?.Invoke(false);
        return;
      }

      Server_HandleEndInteract(ClientManager.Connection);
    }

    [Server]
    public void ServerForceInteract(NetworkConnection conn) => HandleInteract(conn, true);

    [ServerRpc(RequireOwnership = false)]
    private void Server_HandleInteract(NetworkConnection conn) => HandleInteract(conn);

    private void HandleInteract(NetworkConnection conn, bool force = false)
    {
      if (!_interactableEnabled || _occupied.Value || conn == null)
      {
        OnInteractionCallbackFromServer(conn, false);
        return;
      }

      _occupiedConnectionId = conn.ClientId;
      _occupied.Value = true;
      OnInteract(conn, force);
      if (!ManualRelease)
        _occupied.Value = false;
      OnInteractionCallbackFromServer(conn, true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void Server_HandleEndInteract(NetworkConnection conn)
    {
      if (!_manualRelease || !_occupied.Value)
      {
        OnEndInteractionCallbackFromServer(conn, false);
        return;
      }

      OnEndInteract(conn);
      _occupied.Value = false;
      OnEndInteractionCallbackFromServer(conn, true);
    }

    [Server]
    public void ReleaseInteractable()
    {
      _occupied.Value = false;
      OnEndInteract();
    }

    protected internal virtual void OnInteract(NetworkConnection conn, bool force)
    {
      GiveOwnership(conn);
    }

    protected internal virtual void OnEndInteract(NetworkConnection conn = null)
    {
      OnInteractEndOnServer?.Invoke();
      _occupiedConnectionId = -1;
      RemoveOwnership();
    }

    protected void RegisterBoolSlot(string key, bool initial = default)
    {
      if (_boolSlots.ContainsKey(key)) return;
      var sv = new SyncVar<bool>(new SyncTypeSettings
      {
        WritePermission = WritePermission.ServerOnly,
        ReadPermission = ReadPermission.Observers
      });
      sv.Value = initial;
      sv.OnChange += (prev, next, asServer) => OnSyncedChanged?.Invoke(key, prev, next, asServer);
      _boolSlots.Add(key, sv);
    }

    protected void RegisterIntSlot(string key, int initial = default)
    {
      if (_intSlots.ContainsKey(key)) return;
      var sv = new SyncVar<int>(new SyncTypeSettings
      {
        WritePermission = WritePermission.ServerOnly,
        ReadPermission = ReadPermission.Observers
      });
      sv.Value = initial;
      sv.OnChange += (prev, next, asServer) => OnSyncedChanged?.Invoke(key, prev, next, asServer);
      _intSlots.Add(key, sv);
    }

    protected void RegisterFloatSlot(string key, float initial = default)
    {
      if (_floatSlots.ContainsKey(key)) return;
      var sv = new SyncVar<float>(new SyncTypeSettings
      {
        WritePermission = WritePermission.ServerOnly,
        ReadPermission = ReadPermission.Observers
      });
      sv.Value = initial;
      sv.OnChange += (prev, next, asServer) => OnSyncedChanged?.Invoke(key, prev, next, asServer);
      _floatSlots.Add(key, sv);
    }

    protected void RegisterStringSlot(string key, string initial = default)
    {
      if (_stringSlots.ContainsKey(key)) return;
      var sv = new SyncVar<string>(new SyncTypeSettings
      {
        WritePermission = WritePermission.ServerOnly,
        ReadPermission = ReadPermission.Observers
      });
      sv.Value = initial;
      sv.OnChange += (prev, next, asServer) => OnSyncedChanged?.Invoke(key, prev, next, asServer);
      _stringSlots.Add(key, sv);
    }

    public bool GetBool(string key) =>
      _boolSlots.TryGetValue(key, out var o) ? ((SyncVar<bool>)o).Value : default;

    public int GetInt(string key) =>
      _intSlots.TryGetValue(key, out var o) ? ((SyncVar<int>)o).Value : default;

    public float GetFloat(string key) =>
      _floatSlots.TryGetValue(key, out var o) ? ((SyncVar<float>)o).Value : default;

    public string GetString(string key) =>
      _stringSlots.TryGetValue(key, out var o) ? ((SyncVar<string>)o).Value : default;

    [Server]
    protected void SetBool(string key, bool v)
    {
      ((SyncVar<bool>)_boolSlots[key]).Value = v;
    }

    [Server]
    protected void SetInt(string key, int v)
    {
      ((SyncVar<int>)_intSlots[key]).Value = v;
    }

    [Server]
    protected void SetFloat(string key, float v)
    {
      ((SyncVar<float>)_floatSlots[key]).Value = v;
    }

    [Server]
    protected void SetString(string key, string v)
    {
      ((SyncVar<string>)_stringSlots[key]).Value = v;
    }

    [ServerRpc(RequireOwnership = false)]
    protected void RequestSetBool(string key, bool v, NetworkConnection caller = null)
    {
      if (!IsServer) return;
      if (!_boolSlots.ContainsKey(key)) return;
      SetBool(key, v);
    }

    [ServerRpc(RequireOwnership = false)]
    protected void RequestSetInt(string key, int v, NetworkConnection caller = null)
    {
      if (!IsServer) return;
      if (!_intSlots.ContainsKey(key)) return;
      SetInt(key, v);
    }

    [ServerRpc(RequireOwnership = false)]
    protected void RequestSetFloat(string key, float v, NetworkConnection caller = null)
    {
      if (!IsServer) return;
      if (!_floatSlots.ContainsKey(key)) return;
      SetFloat(key, v);
    }

    [ServerRpc(RequireOwnership = false)]
    protected void RequestSetString(string key, string v, NetworkConnection caller = null)
    {
      if (!IsServer) return;
      if (!_stringSlots.ContainsKey(key)) return;
      SetString(key, v);
    }

    public void ForceApplyAll()
    {
      OnForceApply?.Invoke();
      OnSyncedChanged?.Invoke("occupied", _occupied.Value, _occupied.Value, false);
      foreach (var kv in _boolSlots)
      {
        var sv = (SyncVar<bool>)kv.Value;
        OnSyncedChanged?.Invoke(kv.Key, sv.Value, sv.Value, false);
      }

      foreach (var kv in _intSlots)
      {
        var sv = (SyncVar<int>)kv.Value;
        OnSyncedChanged?.Invoke(kv.Key, sv.Value, sv.Value, false);
      }

      foreach (var kv in _floatSlots)
      {
        var sv = (SyncVar<float>)kv.Value;
        OnSyncedChanged?.Invoke(kv.Key, sv.Value, sv.Value, false);
      }

      foreach (var kv in _stringSlots)
      {
        var sv = (SyncVar<string>)kv.Value;
        OnSyncedChanged?.Invoke(kv.Key, sv.Value, sv.Value, false);
      }
    }

    public byte[] CaptureHostState()
    {
      if (!IsServer) return Array.Empty<byte>();
      using var ms = new MemoryStream();
      using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: false);

      bw.Write(MIGRATION_VERSION);
      bw.Write(IsOccupied);

      bw.Write(_boolSlots.Count);
      foreach (var kv in _boolSlots)
      {
        var sv = (SyncVar<bool>)kv.Value;
        bw.Write(kv.Key);
        bw.Write(sv.Value);
      }

      bw.Write(_intSlots.Count);
      foreach (var kv in _intSlots)
      {
        var sv = (SyncVar<int>)kv.Value;
        bw.Write(kv.Key);
        bw.Write(sv.Value);
      }

      bw.Write(_floatSlots.Count);
      foreach (var kv in _floatSlots)
      {
        var sv = (SyncVar<float>)kv.Value;
        bw.Write(kv.Key);
        bw.Write(sv.Value);
      }

      bw.Write(_stringSlots.Count);
      foreach (var kv in _stringSlots)
      {
        var sv = (SyncVar<string>)kv.Value;
        bw.Write(kv.Key);
        bw.Write(sv.Value ?? string.Empty);
      }

      bw.Flush();
      return ms.ToArray();
    }

    [Server]
    public void RestoreHostState(byte[] data)
    {
      if (data == null || data.Length == 0) return;

      using var ms = new MemoryStream(data);
      using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: false);

      int ver = br.ReadInt32();
      if (ver != MIGRATION_VERSION) return;

      bool occupied = br.ReadBoolean();

      int bCount = br.ReadInt32();
      for (int i = 0; i < bCount; i++)
      {
        string key = br.ReadString();
        bool val = br.ReadBoolean();
        if (!_boolSlots.ContainsKey(key)) RegisterBoolSlot(key, val);
        SetBool(key, val);
      }

      int iCount = br.ReadInt32();
      for (int i = 0; i < iCount; i++)
      {
        string key = br.ReadString();
        int val = br.ReadInt32();
        if (!_intSlots.ContainsKey(key)) RegisterIntSlot(key, val);
        SetInt(key, val);
      }

      int fCount = br.ReadInt32();
      for (int i = 0; i < fCount; i++)
      {
        string key = br.ReadString();
        float val = br.ReadSingle();
        if (!_floatSlots.ContainsKey(key)) RegisterFloatSlot(key, val);
        SetFloat(key, val);
      }

      int sCount = br.ReadInt32();
      for (int i = 0; i < sCount; i++)
      {
        string key = br.ReadString();
        string val = br.ReadString();
        if (!_stringSlots.ContainsKey(key)) RegisterStringSlot(key, val);
        SetString(key, val);
      }

      ForceApplyAll();
    }

    public delegate void OnInteractCallback(bool success);

    public event OnInteractCallback InteractCallback;
    public event Action OnInteractEndOnServer;

    [TargetRpc]
    private void OnInteractionCallbackFromServer(NetworkConnection target, bool success)
      => InteractCallback?.Invoke(success);

    [TargetRpc]
    private void OnEndInteractionCallbackFromServer(NetworkConnection target, bool success)
      => InteractCallback?.Invoke(success);
  }
}