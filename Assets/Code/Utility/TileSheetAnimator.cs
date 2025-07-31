using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class TileSheetAnimator : MonoBehaviour
{
    public enum FrameOrder
    {
        LeftToRight_BottomToTop, // индекс 0 — внизу слева, потом вправо, затем вверх
        LeftToRight_TopToBottom  // индекс 0 — вверху слева, потом вправо, затем вниз
    }

    [Header("Tile sheet layout")]
    [Tooltip("Количество столбцов в атласе")]
    public int columns = 4;
    [Tooltip("Количество строк в атласе")]
    public int rows = 4;

    [Header("Frame range (0-based, включительно)")]
    [Tooltip("Начальный индекс тайла")]
    public int startIndex = 0;
    [Tooltip("Конечный индекс тайла")]
    public int endIndex = 3;

    [Header("Playback")]
    [Tooltip("Кадров в секунду")]
    public float fps = 10f;
    [Tooltip("Зациклить")]
    public bool loop = true;
    [Tooltip("Запустить автоматически")]
    public bool playOnAwake = true;
    [Tooltip("Если true — инстанцирует материал, чтобы не мутировать sharedMaterial")]
    public bool useInstanceMaterial = true;
    [Tooltip("Если true — скрипт переустанавливает тайлинг (scale) под размер одного тайла. Если false — оставляет тот, что в материале, и крутит только offset.")]
    public bool overrideTiling = true;
    [Tooltip("Порядок, как читаются тайлы по вертикали")]
    public FrameOrder frameOrder = FrameOrder.LeftToRight_BottomToTop;

    // Событие при завершении цикла (если loop = true, вызывается каждый раз)
    public event System.Action OnLoopComplete;

    // внутренние
    private Renderer _renderer;
    private Material _material;
    private string _texProp; // "_BaseMap" или "_MainTex"
    private float _timePerFrame;
    private float _accum;
    private int[] _sequence;
    private int _currentSeqIndex;
    private bool _playing;

    // чтобы восстановить оригинальный scale, если overrideTiling = false
    private Vector2 _originalScale;
    private bool _lastOverrideTiling;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (useInstanceMaterial)
            _material = _renderer.material; // создаёт instance
        else
            _material = _renderer.sharedMaterial;

        if (_material == null)
        {
            Debug.LogError("TileSheetAnimator: нет материала на рендерере.");
            enabled = false;
            return;
        }

        // выбрать нужное свойство текстуры
        if (_material.HasProperty("_BaseMap"))
            _texProp = "_BaseMap";
        else if (_material.HasProperty("_MainTex"))
            _texProp = "_MainTex";
        else
        {
            Debug.LogWarning("Материал не содержит _BaseMap или _MainTex, стили offset/scale не применятся.");
            _texProp = "_MainTex"; // всё равно пробуем
        }

        // запомним оригинальный scale (чтобы при overrideTiling = false вернуть его)
        _originalScale = _material.GetTextureScale(_texProp);
        _lastOverrideTiling = overrideTiling;

        // установка scale (если нужно)
        UpdateScale();

        BuildSequence();
        _timePerFrame = fps > 0f ? 1f / fps : float.PositiveInfinity;

        if (playOnAwake)
            Play();
        else
            ApplyCurrentTileImmediate();
    }

    void Update()
    {
        // отслеживаем переключение опции в рантайме
        if (overrideTiling != _lastOverrideTiling)
        {
            UpdateScale();
            _lastOverrideTiling = overrideTiling;
        }

        if (!_playing || fps <= 0f || _sequence == null || _sequence.Length == 0)
            return;

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
            if (loop)
            {
                _currentSeqIndex = 0;
                OnLoopComplete?.Invoke();
            }
            else
            {
                _playing = false;
                _currentSeqIndex = _sequence.Length - 1;
            }
        }
    }

    private void ApplyCurrentTileImmediate()
    {
        if (_sequence != null && _sequence.Length > 0)
            SetTile(_sequence[_currentSeqIndex]);
    }

    private void UpdateScale()
    {
        if (_material == null)
            return;

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
        int e = Mathf.Clamp(endIndex, 0, tileCount - 1);

        if (s == e)
        {
            _sequence = new[] { s };
        }
        else if (s < e)
        {
            _sequence = new int[e - s + 1];
            for (int i = 0; i < _sequence.Length; i++)
                _sequence[i] = s + i;
        }
        else // s > e — убывающая последовательность
        {
            _sequence = new int[s - e + 1];
            for (int i = 0; i < _sequence.Length; i++)
                _sequence[i] = s - i;
        }

        _currentSeqIndex = 0;
    }

    private void SetTile(int index)
    {
        int tileCount = Mathf.Max(1, columns * rows);
        if (index < 0 || index >= tileCount)
            return;

        int col = index % columns;
        int row = index / columns;

        Vector2 offset;
        switch (frameOrder)
        {
            case FrameOrder.LeftToRight_TopToBottom:
                // строка 0 — сверху
                offset = new Vector2((float)col / columns, 1f - ((float)row + 1f) / rows);
                break;
            case FrameOrder.LeftToRight_BottomToTop:
            default:
                // строка 0 — снизу
                offset = new Vector2((float)col / columns, (float)row / rows);
                break;
        }

        _material.SetTextureOffset(_texProp, offset);
    }

    /// <summary>Запустить анимацию (с текущего или начального кадра).</summary>
    public void Play()
    {
        if (_sequence == null || _sequence.Length == 0)
            BuildSequence();

        _playing = true;
        _accum = 0f;
        _timePerFrame = fps > 0f ? 1f / fps : float.PositiveInfinity;
        _currentSeqIndex = 0;
        ApplyCurrentTileImmediate();
    }

    /// <summary>Поставить на паузу.</summary>
    public void Pause() => _playing = false;

    /// <summary>Остановить и вернуться к стартовому тайлу.</summary>
    public void Stop()
    {
        _playing = false;
        _currentSeqIndex = 0;
        ApplyCurrentTileImmediate();
    }

    /// <summary>Задать новый диапазон и пересобрать последовательность.</summary>
    public void SetRange(int newStart, int newEnd)
    {
        startIndex = newStart;
        endIndex = newEnd;
        BuildSequence();
        ApplyCurrentTileImmediate();
    }

    /// <summary>Перейти на конкретный индекс (если он входит в текущую последовательность).</summary>
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
    void OnValidate()
    {
        if (columns < 1) columns = 1;
        if (rows < 1) rows = 1;

        // пересобираем последовательность и обновляем scale в редакторе
        BuildSequence();
        UpdateScale();

        if (_material != null)
        {
            ApplyCurrentTileImmediate();
        }
    }
#endif
}
