using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class UVDiscoFloor : MonoBehaviour
{
    [SerializeField] private int materialIndex = 0;
    [SerializeField, Min(0f)] private float interval = 1f;
    [SerializeField, Tooltip("Имя текстурного слота. Пусто = авто (_BaseMap/_MainTex).")]
    private string textureProperty = "";

    private static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId   = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");

    private static readonly float[] kRecipPow2 = { 0.5f, 0.25f, 0.125f, 0.0625f, 0.03125f };

    private Renderer _rend;
    private MaterialPropertyBlock _mpb;

    private Vector2 _baseTiling;
    private Vector2 _baseOffset;
    private int _stId;
    private string _texProp;

    private float _timer;
    private bool _isVisible;
    private bool _isActive = true; // 🔹 Новое поле — эффект включён по умолчанию

    private void Awake()
    {
        _rend = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (!InitForMaterial())
        {
            enabled = false;
            return;
        }
        CacheBaseUV();

        _timer = Random.value * interval;
        if (interval == 0f) _timer = 0f;
    }

    private bool InitForMaterial()
    {
        var sharedMats = _rend.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= sharedMats.Length)
        {
            Debug.LogWarning($"[{nameof(UVDiscoFloor)}] Некорректный materialIndex={materialIndex} на {gameObject.name}");
            return false;
        }

        var mat = sharedMats[materialIndex];
        if (mat == null)
        {
            Debug.LogWarning($"[{nameof(UVDiscoFloor)}] Пустой материал на индексе {materialIndex} у {gameObject.name}");
            return false;
        }

        if (!string.IsNullOrEmpty(textureProperty))
        {
            _texProp = textureProperty;
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
            Debug.LogWarning($"[{nameof(UVDiscoFloor)}] У шейдера {mat.name} нет _BaseMap/_MainTex. Укажите слот вручную.");
            return false;
        }

        return true;
    }

    private void CacheBaseUV()
    {
        // 🔹 Запоминаем исходное состояние UV
        var mat = _rend.sharedMaterials[materialIndex];
        _baseTiling = mat.GetTextureScale(_texProp);
        _baseOffset = mat.GetTextureOffset(_texProp);
    }

    private void Update()
    {
        if (!_isVisible || !_isActive)
            return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        float ox = kRecipPow2[Random.Range(0, kRecipPow2.Length)];
        float oy = kRecipPow2[Random.Range(0, kRecipPow2.Length)];

        Vector2 newOffset = new Vector2(ox, oy);
        var st = new Vector4(_baseTiling.x, _baseTiling.y, _baseOffset.x + newOffset.x, _baseOffset.y + newOffset.y);

        _mpb.SetVector(_stId, st);
        _rend.SetPropertyBlock(_mpb, materialIndex);

        _timer = (interval > 0f) ? interval : 0f;
    }

    private void OnBecameInvisible() => _isVisible = false;
    private void OnBecameVisible() => _isVisible = true;

    private void OnDisable()
    {
        ResetToBaseUV();
    }

    private void ResetToBaseUV()
    {
        // 🔹 Возвращаем исходное состояние UV
        if (_rend == null) return;

        _mpb.SetVector(_stId, new Vector4(_baseTiling.x, _baseTiling.y, _baseOffset.x, _baseOffset.y));
        _rend.SetPropertyBlock(_mpb, materialIndex);
    }

    // =========================
    // 🔹 Публичные методы API
    // =========================

    /// <summary>Включает/выключает анимацию UV.</summary>
    public void SetActive(bool state)
    {
        if (_isActive == state) return;

        _isActive = state;
        if (!_isActive)
        {
            ResetToBaseUV();
        }
        else
        {
            _timer = interval; // перезапуск таймера
        }
    }

    public bool IsActive => _isActive;

    public void SetInterval(float newInterval) => interval = Mathf.Max(0f, newInterval);

    public void SetMaterialIndex(int index)
    {
        materialIndex = index;
        if (enabled) OnEnable();
    }
}
