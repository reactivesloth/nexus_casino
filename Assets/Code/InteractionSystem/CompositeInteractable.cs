using System;
using System.Linq;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;

namespace Code.InteractionSystem
{
    public class CompositeInteractable : Interactable
    {
        [Header("Children to interact with")]
        [SerializeField, Tooltip("Все дочерние Interactable, которые нужно задействовать за одно нажатие.")]
        private Interactable[] children;
        
        // Вспомогательное поле для авто-сгенерированного коллайдера
        private BoxCollider _compositeCollider;
        [SerializeField] private bool generateColliderFromChildren = true;
        private void Awake()
        {
            // Настраиваем свой BoxCollider-триггер
            _compositeCollider = GetComponent<BoxCollider>();
            if (_compositeCollider != null)
                _compositeCollider.isTrigger = true;
            
            if (generateColliderFromChildren && _compositeCollider != null)
                UpdateCompositeColliderBounds();
        }

#if UNITY_EDITOR
        // Чтобы сразу видеть в сцене границы
        private void OnDrawGizmosSelected()
        {
            if (_compositeCollider == null) return;
            Gizmos.color = Color.yellow;
            Vector3 worldCenter = transform.TransformPoint(_compositeCollider.center);
            Vector3 worldSize = Vector3.Scale(_compositeCollider.size, transform.lossyScale);
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }
#endif

        /// <summary>
        /// Собираем одну общую “обёртку” над всеми коллайдерами детей
        /// </summary>
        private void UpdateCompositeColliderBounds()
        {
            // Берём все коллайдеры внутри, кроме своего
            var childCols = GetComponentsInChildren<Collider>()
                .Where(c => c != _compositeCollider)
                .ToArray();
            if (childCols.Length == 0) return;

            // Считаем объединённые мировые границы
            var bounds = childCols[0].bounds;
            for (int i = 1; i < childCols.Length; i++)
                bounds.Encapsulate(childCols[i].bounds);

            // Центр в локальных координатах
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            _compositeCollider.center = localCenter;

            // Размер берём из world size, переводим в local, учитывая scale
            Vector3 worldSize = bounds.size;
            Vector3 localSize = new Vector3(
                worldSize.x / transform.lossyScale.x,
                worldSize.y / transform.lossyScale.y,
                worldSize.z / transform.lossyScale.z
            );
            _compositeCollider.size = localSize;
        }

        public override string InteractionPrompt
        {
            get
            {
                if (!IsEnabled) return "Disabled";
                if (IsOccupied)  return ManualRelease ? "Press E to end" : "Occupied";
                var prompts = children
                    .Where(c => c.IsEnabled && !c.IsOccupied)
                    .Select(c => c.InteractionPrompt)
                    .ToArray();
                return prompts.Length > 0
                    ? string.Join(" + ", prompts)
                    : base.InteractionPrompt;
            }
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            foreach (var child in children)
                child.OnInteract(conn);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            foreach (var child in children)
                child.OnEndInteract(conn);
        }

        private void Update()
        {
            IsBusy = false;
            foreach (var child in children)
                if (child.IsBusy)
                    IsBusy = true;
        }

        [Server]
        public void ReleaseAll()
        {
            foreach (var child in children)
                child.ReleaseInteractable();
            ReleaseInteractable();
        }
    }
}
