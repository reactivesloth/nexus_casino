#if !UNITY_SERVER
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class UVScroller : MonoBehaviour
{
    [SerializeField] private Vector2 scrollSpeed = Vector2.zero;
    [SerializeField] private int materialIndex = 0;
    [SerializeField] private bool randomizePhase = true;
    [SerializeField, Tooltip("Имя текстурного слота. Пусто = авто (_BaseMap или _MainTex).")]
    private string textureProperty = "";

    private Renderer _rend;
    private MaterialPropertyBlock _mpb;

    private int _stId;                  // *_ST property id
    private string _texProp;            // "_BaseMap" или "_MainTex"
    private Vector2 _baseTiling;        // исходный тайлинг материала
    private Vector2 _baseOffset;        // исходный оффсет материала
    private float _phase;               // случайный сдвиг времени

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

    private bool _isVisible;

    private void Awake()
    {
        _rend = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        if (randomizePhase) _phase = Random.Range(0f, 100f);
    }

    private void OnEnable()
    {
        if (!InitForMaterial())
        {
            enabled = false; // корректно выключаемся, если что-то не так
            return;
        }

        // Если скорость нулевая — незачем крутить Update
        if (scrollSpeed.sqrMagnitude < 1e-8f)
            enabled = false;
    }

    private bool InitForMaterial()
    {
        var sharedMats = _rend.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= sharedMats.Length)
        {
            Debug.LogWarning($"[{nameof(UVScroller)}] Некорректный materialIndex={materialIndex} на {gameObject.name}");
            return false;
        }

        var mat = sharedMats[materialIndex];
        if (mat == null)
        {
            Debug.LogWarning($"[{nameof(UVScroller)}] Пустой материал по индексу {materialIndex} на {gameObject.name}");
            return false;
        }

        // Определяем, какой текстурный слот есть у шейдера
        if (!string.IsNullOrEmpty(textureProperty))
        {
            _texProp = textureProperty;
            // если пользователь задал кастомный слот, попытаемся угадать ST-id
            _stId = (_texProp == "_BaseMap") ? BaseMapStId :
                    (_texProp == "_MainTex") ? MainTexStId :
                    Shader.PropertyToID(_texProp + "_ST");
        }
        else if (mat.HasProperty(BaseMapId))
        {
            _texProp = "_BaseMap";
            _stId = BaseMapStId;
        }
        else if (mat.HasProperty(MainTexId))
        {
            _texProp = "_MainTex";
            _stId = MainTexStId;
        }
        else
        {
            Debug.LogWarning($"[{nameof(UVScroller)}] У материала {mat.name} нет _BaseMap/_MainTex. Укажите слот вручную.");
            return false;
        }

        // Читаем базовые значения только один раз с SHARED материала
        _baseTiling = mat.GetTextureScale(_texProp);
        _baseOffset = mat.GetTextureOffset(_texProp);
        return true;
    }

    private void Update()
    {
        if (!_isVisible) return;
        // без аллокаций: только арифметика и SetPropertyBlock
        float t = Time.time + _phase;
        Vector2 dynamicOffset = _baseOffset + t * scrollSpeed;

        // *_ST: xy = tiling, zw = offset
        var st = new Vector4(_baseTiling.x, _baseTiling.y, dynamicOffset.x, dynamicOffset.y);
        _mpb.SetVector(_stId, st);

        // важнo: используем перегрузку с индексом, чтобы не трогать другие сабмеши
        _rend.SetPropertyBlock(_mpb, materialIndex);
    }
    
    private void OnBecameInvisible()
    {
        _isVisible = false;
    }

    private void OnBecameVisible()
    {
        _isVisible = true;
    }
    
    private void OnDisable()
    {
        // очищаем MPB для этого индекса, чтобы вернуть исходные UV
        if (_rend != null)
            _rend.SetPropertyBlock(null, materialIndex);
    }

    /// <summary>Позволяет менять скорость в рантайме без аллокаций.</summary>
    public void SetSpeed(Vector2 speed)
    {
        scrollSpeed = speed;
        if (!enabled && scrollSpeed.sqrMagnitude >= 1e-8f)
            enabled = true;
    }
}
#endif