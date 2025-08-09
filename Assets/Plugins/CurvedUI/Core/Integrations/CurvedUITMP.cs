using System.Collections.Generic;
using CurvedUI.Core.Utilities;
using TMPro;
using UnityEngine;
[assembly: OptionalDependency("TMPro.TextMeshProUGUI", "CURVEDUI_TMP")]

namespace CurvedUI.Core.Integrations
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(110)]
    public class CurvedUITMP : MonoBehaviour
    {
#if CURVEDUI_TMP || TMP_PRESENT
        // internals
        private CurvedUIVertexEffect crvdVE;
        private TextMeshProUGUI tmpText;
        private CurvedUISettings mySettings;
        private CanvasRenderer canvasRenderer;

        // reusable buffers (без выделений каждый кадр)
        private List<UIVertex> m_UIVerts = new();
        private UIVertex m_tempVertex;
        private CurvedUITMPSubmesh m_tempSubMsh;

        private Vector2 savedSize;
        private Vector3 savedUp;
        private Vector3 savedPos;
        private Vector3 savedLocalScale;
        private Vector3 savedGlobalScale;
        private readonly List<CurvedUITMPSubmesh> subMeshes = new();

        // flags
        public bool Dirty; // внешняя принудительная перестройка
        private bool curvingRequired;
        private bool tesselationRequired;
        private bool quitting;

        // текущий массив вершин главного меша TMP (ссылка на внутренний буфер TMP)
        private Vector3[] verticesRef;

        private void Start()
        {
            if (mySettings == null)
                mySettings = GetComponentInParent<CurvedUISettings>();
        }

        private void OnEnable()
        {
            FindTMP();

            if (tmpText)
            {
                tmpText.RegisterDirtyMaterialCallback(TesselationRequiredCallback);
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TMPTextChangedCallback);

                // триггерим первичную перестройку
                tmpText.SetText(tmpText.text);
                Dirty = true;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update += LateUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update -= LateUpdate;
#endif
            if (tmpText)
            {
                tmpText.UnregisterDirtyMaterialCallback(TesselationRequiredCallback);
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TMPTextChangedCallback);
            }

            // Важно: отпускаем CanvasRenderer, чтобы он не держал ссылку на прежний меш.
            if (canvasRenderer != null)
                canvasRenderer.Clear();

            verticesRef = null;
        }

        private void OnDestroy()
        {
            quitting = true;
        }

        private void LateUpdate()
        {
            if (!tmpText) FindTMP();
            if (mySettings == null || tmpText == null || quitting) return;

            if (ShouldTesselate())
                tesselationRequired = true;

            // Перестраиваем только при необходимости
            if (Dirty || tesselationRequired || (curvingRequired && !Application.isPlaying))
            {
                if (mySettings == null)
                {
                    enabled = false;
                    return;
                }

                // Обновляем textInfo, но без лишних аллокаций
                tmpText.renderMode = TextRenderFlags.Render;
                tmpText.ForceMeshUpdate(false, false); // не игнорируем activeState и не создаём новые массивы

                // Получаем ПРЯМЫЕ ссылки на массивы вершин TMP (никаких mesh.vertices!)
                // Главный сабмеш для основного текста — index 0
                var ti = tmpText.textInfo;
                if (ti.meshInfo == null || ti.meshInfo.Length == 0)
                {
                    canvasRenderer?.Clear();
                    tmpText.renderMode = TextRenderFlags.DontRender;
                    ResetSavedTransformData();
                    ResetFlags();
                    return;
                }

                var mi0 = ti.meshInfo[0];
                verticesRef = mi0.vertices; // это ссылка на внутренний массив TMP

                // Сформировать/обновить список UIVertex без аллокаций
                CreateUIVertexListFromVerticesArray(verticesRef);

                // Применяем кривизну (в твоём эффекте ничего менять не нужно)
                crvdVE.ModifyTMPMesh(ref m_UIVerts);

                // Записываем изменённые позиции обратно в тот же массив
                for (int i = 0; i < m_UIVerts.Count; i++)
                    verticesRef[i] = m_UIVerts[i].position;

                // Обновляем отрисовку без SetMesh и без RecalculateBounds
                tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);

                // Запоминаем параметры кривизны/трансформа для ShouldTesselate()
                savedLocalScale = mySettings.transform.localScale;
                savedGlobalScale = mySettings.transform.lossyScale;
                savedSize = ((RectTransform)transform).rect.size;
                savedUp = mySettings.transform.worldToLocalMatrix.MultiplyVector(transform.up);
                savedPos = mySettings.transform.worldToLocalMatrix.MultiplyPoint3x4(transform.position);

                // Сбрасываем флаги
                ResetFlags();

                // Обновляем сабмеши TMP (спрайты/маттеги) без создания новых мешей
                FindSubmeshes();
                for (int i = 0; i < subMeshes.Count; i++)
                    subMeshes[i].UpdateSubmesh(true, false);

                // снимаем рендер у модуля TMP, отрисовка уже в CanvasRenderer
                tmpText.renderMode = TextRenderFlags.DontRender;
            }

            // Если текста нет — чистим CanvasRenderer (важно для удержания ссылок)
            if (tmpText.text.Length == 0) canvasRenderer?.Clear();
        }

        // ---------- UIVERTEX (без копий mesh.vertices) ----------
        private void CreateUIVertexListFromVerticesArray(Vector3[] verts)
        {
            // подрезаем список, если вершин стало меньше
            if (verts.Length < m_UIVerts.Count)
                m_UIVerts.RemoveRange(verts.Length, m_UIVerts.Count - verts.Length);

            for (int i = 0; i < verts.Length; i++)
            {
                if (m_UIVerts.Count <= i)
                {
                    // добавляем новый UIVertex без дополнительной инициализации
                    m_tempVertex = new UIVertex { position = verts[i] };
                    m_UIVerts.Add(m_tempVertex);
                }
                else
                {
                    m_tempVertex = m_UIVerts[i];
                    m_tempVertex.position = verts[i];
                    m_UIVerts[i] = m_tempVertex;
                }
            }
        }

        // ---------- PRIVATE ----------
        private void FindTMP()
        {
            if (GetComponent<TextMeshProUGUI>() == null) return;

            tmpText = GetComponent<TextMeshProUGUI>();
            crvdVE = GetComponent<CurvedUIVertexEffect>();
            mySettings = GetComponentInParent<CurvedUISettings>();
            canvasRenderer = tmpText != null ? tmpText.canvasRenderer : null;
            transform.hasChanged = false;

            FindSubmeshes();
        }

        private void FindSubmeshes()
        {
            subMeshes.Clear();
            var subs = GetComponentsInChildren<TMP_SubMeshUI>(true);
            for (int i = 0; i < subs.Length; i++)
            {
                m_tempSubMsh = subs[i].gameObject.AddComponentIfMissing<CurvedUITMPSubmesh>();
                if (!subMeshes.Contains(m_tempSubMsh))
                    subMeshes.Add(m_tempSubMsh);
            }
        }

        private bool ShouldTesselate()
        {
            if (savedSize != ((RectTransform)transform).rect.size) return true;
            if (savedLocalScale != mySettings.transform.localScale) return true;
            if (savedGlobalScale != mySettings.transform.lossyScale) return true;
            if (!savedUp.AlmostEqual(mySettings.transform.worldToLocalMatrix.MultiplyVector(transform.up))) return true;

            var testedPos = mySettings.transform.worldToLocalMatrix.MultiplyPoint3x4(transform.position);
            if (!savedPos.AlmostEqual(testedPos))
            {
                if (mySettings.Shape != CurvedUISettings.CurvedUIShape.CYLINDER ||
                    Mathf.Pow(testedPos.x - savedPos.x, 2) > 0.00001f ||
                    Mathf.Pow(testedPos.z - savedPos.z, 2) > 0.00001f)
                {
                    return true;
                }
            }
            return false;
        }

        private void ResetSavedTransformData()
        {
            savedLocalScale = mySettings.transform.localScale;
            savedGlobalScale = mySettings.transform.lossyScale;
            savedSize = ((RectTransform)transform).rect.size;
            savedUp = mySettings.transform.worldToLocalMatrix.MultiplyVector(transform.up);
            savedPos = mySettings.transform.worldToLocalMatrix.MultiplyPoint3x4(transform.position);
        }

        private void ResetFlags()
        {
            tesselationRequired = false;
            curvingRequired = false;
            Dirty = false;
        }

        // ---------- EVENTS ----------
        private void TMPTextChangedCallback(object obj)
        {
            if (obj != (object)tmpText) return;
            tesselationRequired = true;
        }

        private void TesselationRequiredCallback()
        {
            tesselationRequired = true;
            curvingRequired = true;
        }
#endif
    }
}
