using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Code.Network.HostMigration;

namespace Code.InteractionSystem
{
  public abstract class Interactable : NetworkBehaviour, IMigratableBase
  {
    [Header("Base settings")]
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private bool _interactableEnabled = true;
    [SerializeField] private bool _manualRelease = false;

    public GameObject[] outlineGameObjects;

    private readonly SyncVar<bool> _occupied = new(new SyncTypeSettings
    {
      WritePermission = WritePermission.ServerOnly,
      ReadPermission  = ReadPermission.Observers
    });

    private const string K_OCCUPIED = "occupied";

    private readonly Dictionary<string, object> _boolSlots   = new();
    private readonly Dictionary<string, object> _intSlots    = new();
    private readonly Dictionary<string, object> _floatSlots  = new();
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

    public override void OnStartServer()
    {
      base.OnStartServer();
      ServerManager.OnRemoteConnectionState += ServerManagerOnRemoteConnectionState;

      _occupied.OnChange += (prev, next, asServer) =>
        OnSyncedChanged?.Invoke(K_OCCUPIED, prev, next, asServer);

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
    private void ServerManagerOnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs stateArgs)
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

    [ServerRpc(RequireOwnership = false)]
    private void Server_HandleInteract(NetworkConnection conn, bool force = false)
    {
      if (!_interactableEnabled || _occupied.Value || conn == null)
      {
        OnInteractionCallbackFromServer(conn, false);
        return;
      }

      GiveOwnership(conn);

      _occupiedConnectionId = conn.ClientId;
      _occupied.Value = true;

      if (IsOwner)
        OnInteract(conn, false);

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

      if (IsOwner)
        OnEndInteract(conn);
      
      OnInteractEndOnServer?.Invoke();
      _occupiedConnectionId = -1;
      _occupied.Value = false;
      RemoveOwnership();
      
      OnEndInteractionCallbackFromServer(conn, true);
    }

    [Server]
    public void ServerForceInteract(NetworkConnection conn, bool force) => HandleInteract(conn, force);

    private void HandleInteract(NetworkConnection conn, bool force = false)
    {
      if (!_interactableEnabled || _occupied.Value || conn == null)
      {
        OnInteractionCallbackFromServer(conn, false);
        return;
      }

      GiveOwnership(conn);

      _occupiedConnectionId = conn.ClientId;
      _occupied.Value = true;
      
      if (IsOwner)
        OnInteract(conn, force);

      if (!ManualRelease)
        _occupied.Value = false;

      OnInteractionCallbackFromServer(conn, true);
    }
    
    [Server]
    public void ReleaseInteractable()
    {
      _occupied.Value = false;
      OnEndInteract();
      OnInteractEndOnServer?.Invoke();
      _occupiedConnectionId = -1;
      _occupied.Value = false;
      RemoveOwnership();
    }

    protected internal abstract void OnInteract(NetworkConnection conn, bool force);

    protected internal abstract void OnEndInteract(NetworkConnection conn = null);

    protected void RegisterBoolSlot(string key, bool initial = default)
    {
      if (_boolSlots.ContainsKey(key)) return;
      var sv = new SyncVar<bool>(new SyncTypeSettings
      {
        WritePermission = WritePermission.ServerOnly,
        ReadPermission  = ReadPermission.Observers
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
        ReadPermission  = ReadPermission.Observers
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
        ReadPermission  = ReadPermission.Observers
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
        ReadPermission  = ReadPermission.Observers
      });
      sv.Value = initial;
      sv.OnChange += (prev, next, asServer) => OnSyncedChanged?.Invoke(key, prev, next, asServer);
      _stringSlots.Add(key, sv);
    }

    public bool   GetBool(string key)   => _boolSlots.TryGetValue(key, out var o)   ? ((SyncVar<bool>)o).Value   : default;
    public int    GetInt(string key)    => _intSlots.TryGetValue(key, out var o)    ? ((SyncVar<int>)o).Value    : default;
    public float  GetFloat(string key)  => _floatSlots.TryGetValue(key, out var o)  ? ((SyncVar<float>)o).Value  : default;
    public string GetString(string key) => _stringSlots.TryGetValue(key, out var o) ? ((SyncVar<string>)o).Value : default;

    [Server] protected void SetBool(string key, bool v)     { ((SyncVar<bool>)_boolSlots[key]).Value     = v; }
    [Server] protected void SetInt(string key, int v)       { ((SyncVar<int>)_intSlots[key]).Value       = v; }
    [Server] protected void SetFloat(string key, float v)   { ((SyncVar<float>)_floatSlots[key]).Value   = v; }
    [Server] protected void SetString(string key, string v) { ((SyncVar<string>)_stringSlots[key]).Value = v; }

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

      OnSyncedChanged?.Invoke(K_OCCUPIED, _occupied.Value, _occupied.Value, false);

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

    [Serializable]
    private class InteractableMigrationData
    {
      public bool occupied;
      public List<KVBool>   bools   = new();
      public List<KVInt>    ints    = new();
      public List<KVFloat>  floats  = new();
      public List<KVString> strings = new();
    }

    [Serializable] private struct KVBool   { public string k; public bool   v; }
    [Serializable] private struct KVInt    { public string k; public int    v; }
    [Serializable] private struct KVFloat  { public string k; public float  v; }
    [Serializable] private struct KVString { public string k; public string v; }

    object IMigratableBase.GetMigrateData()
    {
      var data = new InteractableMigrationData
      {
        occupied = IsOccupied
      };

      foreach (var kv in _boolSlots)
        data.bools.Add(new KVBool { k = kv.Key, v = ((SyncVar<bool>)kv.Value).Value });

      foreach (var kv in _intSlots)
        data.ints.Add(new KVInt { k = kv.Key, v = ((SyncVar<int>)kv.Value).Value });

      foreach (var kv in _floatSlots)
        data.floats.Add(new KVFloat { k = kv.Key, v = ((SyncVar<float>)kv.Value).Value });

      foreach (var kv in _stringSlots)
        data.strings.Add(new KVString { k = kv.Key, v = ((SyncVar<string>)kv.Value).Value ?? string.Empty });

      return data;
    }

    string IMigratableBase.GetJson(object data)
    {
      return JsonUtility.ToJson((InteractableMigrationData)data, prettyPrint: false);
    }

    void IMigratableBase.OnMigrateDataReceived(string jsonData)
    {
      if (!IsServer) return;

      if (string.IsNullOrEmpty(jsonData))
        return;

      InteractableMigrationData data = null;
      try
      {
        data = JsonUtility.FromJson<InteractableMigrationData>(jsonData);
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[Interactable] Failed to parse migration data: {e.Message}", this);
        return;
      }
      if (data == null) return;

      if (data.bools != null)
      {
        for (int i = 0; i < data.bools.Count; i++)
        {
          var kv = data.bools[i];
          if (!_boolSlots.ContainsKey(kv.k)) RegisterBoolSlot(kv.k, kv.v);
          SetBool(kv.k, kv.v);
        }
      }

      if (data.ints != null)
      {
        for (int i = 0; i < data.ints.Count; i++)
        {
          var kv = data.ints[i];
          if (!_intSlots.ContainsKey(kv.k)) RegisterIntSlot(kv.k, kv.v);
          SetInt(kv.k, kv.v);
        }
      }

      if (data.floats != null)
      {
        for (int i = 0; i < data.floats.Count; i++)
        {
          var kv = data.floats[i];
          if (!_floatSlots.ContainsKey(kv.k)) RegisterFloatSlot(kv.k, kv.v);
          SetFloat(kv.k, kv.v);
        }
      }

      if (data.strings != null)
      {
        for (int i = 0; i < data.strings.Count; i++)
        {
          var kv = data.strings[i];
          if (!_stringSlots.ContainsKey(kv.k)) RegisterStringSlot(kv.k, kv.v);
          SetString(kv.k, kv.v);
        }
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