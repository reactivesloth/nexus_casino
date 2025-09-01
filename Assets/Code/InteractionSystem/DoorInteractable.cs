using System;
using System.Collections;
using Code.InteractionSystem;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Doors
{
  public enum DoorMode { ManualOnly, AutoOnly, AutoAndManual }

  [Serializable]
  public class DoorElement
  {
    public Transform transform;
    public Vector3 closedRot;
    public Vector3 openRot;
  }

  public sealed class DoorInteractable : Interactable
  {
    private const string K_TARGET = "door.targetOpen";

    [Header("Mode")]
    [SerializeField] private DoorMode mode = DoorMode.ManualOnly;

    [Header("Door Elements (multiple panels supported)")]
    [SerializeField] private DoorElement[] elements = Array.Empty<DoorElement>();

    [Header("Manual settings")]
    [Tooltip("Скорость изменения степени открытия при Manual (доля в сек).")]
    [SerializeField] private float manualSpeed = 2f;

    [Header("Auto settings")]
    [Tooltip("Дистанция полной закрытости.")]
    [SerializeField] private float closeDistance = 10f;
    [Tooltip("Дистанция полной открытости.")]
    [SerializeField] private float fullOpenDistance = 1f;
    [Tooltip("Порог инерции смены направления (0.01..0.1).")]
    [SerializeField] private float sensitivity = 0.05f;

    [Header("Client visuals")]
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [SerializeField, Tooltip("Время анимации на клиенте, сек")]
    private float animationDuration = 0.5f;

    private float _visualDegree;
    private AnimationCurve _currentCurve;
    private float _prevTarget;
    private int _prevDir;
    private float _accDelta;

    private Transform[] _players = Array.Empty<Transform>();
    private float _scanTimer;
    private float _lastNearest;
    private Coroutine _manualRoutine;
    private bool _isOpen;
    private bool _initedDoor;

    private void EnsureInit()
    {
      if (_initedDoor) return;
      _initedDoor = true;
      if (elements == null) elements = Array.Empty<DoorElement>();
      NormalizeDistances();
      var col = GetComponent<Collider>();
      if (col != null) col.isTrigger = true;
    }

    private void NormalizeDistances()
    {
      if (fullOpenDistance < 0f) fullOpenDistance = 0f;
      if (closeDistance < 0.01f) closeDistance = 0.01f;
      if (fullOpenDistance >= closeDistance)
        closeDistance = fullOpenDistance + 0.01f;
      if (sensitivity < 0.001f) sensitivity = 0.001f;
      if (manualSpeed < 0f) manualSpeed = 0f;
    }

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
      if (_manualRoutine != null) { StopCoroutine(_manualRoutine); _manualRoutine = null; }
    }

    public override void OnStartServer()
    {
      base.OnStartServer();
      NormalizeDistances();
      RegisterFloatSlot(K_TARGET, 0f);
      _prevTarget = 0f;
      SetFloat(K_TARGET, 0f);
    }

    public override void OnStartClient()
    {
      base.OnStartClient();
      EnsureInit();
    }

    private void Update()
    {
      if (IsServer && (mode == DoorMode.AutoOnly || mode == DoorMode.AutoAndManual))
        Server_AutoTick();

      float target = Mathf.Clamp01(GetFloat(K_TARGET));
      float targetCurve = EvaluateByCurve(target);
      float step = (animationDuration > 0f) ? Time.deltaTime / animationDuration : 1f;
      _visualDegree = Mathf.MoveTowards(_visualDegree, targetCurve, step);
      ApplyToElements(_visualDegree);
    }

    #region Manual

    protected internal override void OnInteract(NetworkConnection conn, bool force = false)
    {
      if (mode == DoorMode.AutoOnly) return;

      bool wantOpen = !_isOpen;
      if (_manualRoutine != null) StopCoroutine(_manualRoutine);
      _manualRoutine = StartCoroutine(Server_ManualSet(wantOpen));
    }

    protected internal override void OnEndInteract(NetworkConnection conn = null)
    {
      
    }

    private IEnumerator Server_ManualSet(bool open)
    {
      _isOpen = open;
      float target = open ? 1f : 0f;
      float t = Mathf.Clamp01(GetFloat(K_TARGET));
      float speed = Mathf.Max(0.0001f, manualSpeed);
      
      while (!Mathf.Approximately(t, target))
      {
        float dir = Mathf.Sign(target - t);
        t += dir * speed * Time.deltaTime;
        t = Mathf.Clamp01(t);
        SetFloat(K_TARGET, t);
        yield return null;
      }

      SetFloat(K_TARGET, target);
      _manualRoutine = null;
    }

    #endregion

    #region Auto (server)

    private void Server_AutoTick()
    {
      Server_AutoScanPlayers();

      if (_players.Length == 0)
      {
        _lastNearest = float.MaxValue;
        ApplyTarget(0f);
        return;
      }

      Vector3 doorPos = transform.position;
      float nearest = float.MaxValue;
      for (int i = 0; i < _players.Length; i++)
      {
        var t = _players[i];
        if (t == null) continue;
        Vector3 a = new Vector3(t.position.x, 0f, t.position.z);
        Vector3 b = new Vector3(doorPos.x,   0f, doorPos.z);
        float d = Vector3.Distance(a, b);
        if (d < nearest) nearest = d;
      }
      _lastNearest = nearest;

      if (nearest <= fullOpenDistance)
      {
        ApplyTarget(1f);
        return;
      }
      if (nearest >= closeDistance)
      {
        ApplyTarget(0f);
        return;
      }

      float span = Mathf.Max(0.0001f, closeDistance - fullOpenDistance);
      float t01 = 1f - Mathf.Clamp01((nearest - fullOpenDistance) / span);

      int dir = Math.Sign(t01 - _prevTarget);
      if (_prevDir != 0 && dir != 0 && dir != _prevDir) _accDelta += t01 - _prevTarget;

      float newTarget;
      if (Mathf.Abs(_accDelta) < sensitivity)
      {
        newTarget = _prevTarget;
        dir = _prevDir;
      }
      else
      {
        newTarget = t01;
        _prevDir = dir;
        _accDelta = 0f;
      }

      _prevTarget = newTarget;
      ApplyTarget(newTarget);
    }

    private void Server_AutoScanPlayers()
    {
      _scanTimer -= Time.deltaTime;
      if (_scanTimer > 0f) return;
      _scanTimer = 0.25f;

      var gos = GameObject.FindGameObjectsWithTag("Player");
      int count = (gos != null) ? gos.Length : 0;

      if (count == 0)
      {
        _players = Array.Empty<Transform>();
      }
      else
      {
        if (_players.Length != count) _players = new Transform[count];
        for (int i = 0; i < count; i++)
          _players[i] = gos[i] != null ? gos[i].transform : null;
      }
    }

    [Server]
    private void ApplyTarget(float value01)
    {
      value01 = Mathf.Clamp01(value01);
      SetFloat(K_TARGET, value01);
      _isOpen = value01 >= 0.5f;
    }

    #endregion

    #region Client visuals

    private void HandleForceApply()
    {
      float tgt = Mathf.Clamp01(GetFloat(K_TARGET));
      _currentCurve = (tgt >= _visualDegree) ? openCurve : closeCurve;
      _visualDegree = EvaluateByCurve(tgt);
      ApplyToElements(_visualDegree);
      _prevTarget = tgt;
      _isOpen = tgt >= 0.5f;
    }

    private void HandleSyncedChanged(string key, object prev, object next, bool asServer)
    {
      if (key != K_TARGET) return;
      float p = Convert.ToSingle(prev);
      float n = Convert.ToSingle(next);
      if (n > p) _currentCurve = openCurve;
      else if (n < p) _currentCurve = closeCurve;
      _isOpen = n >= 0.5f;
    }

    private float EvaluateByCurve(float degree01)
    {
      var curve = _currentCurve;
      if (curve == null) curve = (degree01 >= _visualDegree) ? openCurve : closeCurve;
      degree01 = Mathf.Clamp01(degree01);
      return curve != null ? curve.Evaluate(degree01) : degree01;
    }

    private void ApplyToElements(float curveValue)
    {
      if (elements == null) return;
      for (int i = 0; i < elements.Length; i++)
      {
        var e = elements[i];
        if (e == null || e.transform == null) continue;
        Vector3 rot = Vector3.Lerp(e.closedRot, e.openRot, curveValue);
        e.transform.localRotation = Quaternion.Lerp(
          e.transform.localRotation,
          Quaternion.Euler(rot),
          Time.deltaTime * 3f
        );
      }
    }

    #endregion

    #if UNITY_EDITOR
    protected override void OnValidate()
    {
      base.OnValidate();
      if (elements == null) elements = Array.Empty<DoorElement>();
      NormalizeDistances();
      var col = GetComponent<Collider>();
      if (col != null) col.isTrigger = true;
    }
#endif
  }
}
