using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class TileSheetAnimator : MonoBehaviour
{
    public enum FrameOrder { LeftToRight_BottomToTop, LeftToRight_TopToBottom }

    [Header("Tile sheet layout")]
    public int columns = 4;
    public int rows = 4;

    [Header("Frame range (0-based, inclusive)")]
    public int startIndex = 0;
    public int endIndex = 3;

    [Header("Playback")]
    public float fps = 10f;
    public bool loop = true;
    public bool playOnAwake = true;
    public bool useInstanceMaterial = true;
    public bool overrideTiling = true;
    public FrameOrder frameOrder = FrameOrder.LeftToRight_BottomToTop;

    public System.Action OnLoopComplete;

    // internals
    private Renderer _renderer;
    private Material _material;
    private string _texProp;
    private float _timePerFrame;
    private float _accum;
    private int[] _sequence;
    private int _currentSeqIndex;
    private bool _playing;

    private Vector2 _originalScale;
    private bool _lastOverrideTiling;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _material = useInstanceMaterial ? _renderer.material : _renderer.sharedMaterial;

        if (_material == null) { Debug.LogError("TileSheetAnimator: no material."); enabled = false; return; }

        _texProp = _material.HasProperty("_BaseMap") ? "_BaseMap" :
                   _material.HasProperty("_MainTex") ? "_MainTex" : "_MainTex";

        _originalScale = _material.GetTextureScale(_texProp);
        _lastOverrideTiling = overrideTiling;

        UpdateScale();
        BuildSequence();

        _timePerFrame = fps > 0f ? 1f / fps : float.PositiveInfinity;

        if (playOnAwake) Play();
        else ApplyCurrentTileImmediate();
    }

    void Update()
    {
        if (overrideTiling != _lastOverrideTiling)
        {
            UpdateScale();
            _lastOverrideTiling = overrideTiling;
        }

        if (!_playing || fps <= 0f || _sequence == null || _sequence.Length == 0) return;

        _accum += Time.deltaTime;
        while (_accum >= _timePerFrame)
        {
            _accum -= _timePerFrame;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        SetTile(_sequence[_currentSeqIndex]);
        _currentSeqIndex++;
        if (_currentSeqIndex >= _sequence.Length)
        {
            if (loop) { _currentSeqIndex = 0; OnLoopComplete?.Invoke(); }
            else { _playing = false; _currentSeqIndex = _sequence.Length - 1; }
        }
    }

    private void ApplyCurrentTileImmediate()
    {
        if (_sequence != null && _sequence.Length > 0)
            SetTile(_sequence[_currentSeqIndex]);
    }

    private void UpdateScale()
    {
        if (_material == null) return;

        if (overrideTiling)
        {
            Vector2 scale = new Vector2(1f / Mathf.Max(1, columns), 1f / Mathf.Max(1, rows));
            _material.SetTextureScale(_texProp, scale);
        }
        else
        {
            _material.SetTextureScale(_texProp, _originalScale);
        }
    }

    private void BuildSequence()
    {
        int tileCount = Mathf.Max(1, columns * rows);
        int s = Mathf.Clamp(startIndex, 0, tileCount - 1);
        int e = Mathf.Clamp(endIndex,   0, tileCount - 1);

        if (s == e)
        {
            _sequence = new[] { s };
        }
        else if (s < e)
        {
            _sequence = new int[e - s + 1];
            for (int i = 0; i < _sequence.Length; i++) _sequence[i] = s + i;
        }
        else
        {
            _sequence = new int[s - e + 1];
            for (int i = 0; i < _sequence.Length; i++) _sequence[i] = s - i;
        }
        _currentSeqIndex = 0;
    }

    private void SetTile(int index)
    {
        int tileCount = Mathf.Max(1, columns * rows);
        if (index < 0 || index >= tileCount || _material == null) return;

        int col = index % columns;
        int row = index / columns;

        Vector2 offset = frameOrder == FrameOrder.LeftToRight_TopToBottom
            ? new Vector2((float)col / columns, 1f - ((float)row + 1f) / rows) // сверху вниз
            : new Vector2((float)col / columns, (float)row / rows);            // снизу вверх

        _material.SetTextureOffset(_texProp, offset);
    }

    public void Play()
    {
        if (_sequence == null || _sequence.Length == 0) BuildSequence();
        _playing = true;
        _accum = 0f;
        _timePerFrame = fps > 0f ? 1f / fps : float.PositiveInfinity;
        _currentSeqIndex = 0;
        ApplyCurrentTileImmediate();
    }

    public void Pause() => _playing = false;

    public void Stop()
    {
        _playing = false;
        _currentSeqIndex = 0;
        ApplyCurrentTileImmediate();
    }

    public void SetRange(int newStart, int newEnd)
    {
        startIndex = newStart;
        endIndex   = newEnd;
        BuildSequence();
        ApplyCurrentTileImmediate();
    }

    public void JumpToFrame(int frameIndex)
    {
        if (_sequence == null) return;
        for (int i = 0; i < _sequence.Length; i++)
        {
            if (_sequence[i] == frameIndex)
            {
                _currentSeqIndex = i;
                SetTile(frameIndex);
                return;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (columns < 1) columns = 1;
        if (rows    < 1) rows = 1;
        BuildSequence();
        if (_material != null) { UpdateScale(); ApplyCurrentTileImmediate(); }
    }
#endif

    private void OnDestroy()
    {
        if (_material != null && useInstanceMaterial)
        {
            // вернуть оригинальный scale на всякий случай
            if (!overrideTiling) _material.SetTextureScale(_texProp, _originalScale);
            Destroy(_material);
            _material = null;
        }
    }
}
