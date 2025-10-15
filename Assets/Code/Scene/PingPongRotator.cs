using UnityEngine;

namespace Code.Scene
{
    /// <summary>
    /// Компонент для автоматического вращения объекта по принципу ping-pong
    /// Идеально подходит для прожекторов, лазеров и других вращающихся объектов
    /// </summary>
    public class PingPongRotator : MonoBehaviour
    {
        [Header("Настройки вращения")]
        [Tooltip("Включить вращение по оси X")]
        public bool rotateX = false;
    
        [Tooltip("Включить вращение по оси Y")]
        public bool rotateY = true;
    
        [Tooltip("Включить вращение по оси Z")]
        public bool rotateZ = false;
    
        [Header("Параметры амплитуды (градусы)")]
        [Tooltip("Минимальный угол поворота по оси X")]
        public float minAngleX = -45f;
    
        [Tooltip("Максимальный угол поворота по оси X")]
        public float maxAngleX = 45f;
    
        [Tooltip("Минимальный угол поворота по оси Y")]
        public float minAngleY = -60f;
    
        [Tooltip("Максимальный угол поворота по оси Y")]
        public float maxAngleY = 60f;
    
        [Tooltip("Минимальный угол поворота по оси Z")]
        public float minAngleZ = -30f;
    
        [Tooltip("Максимальный угол поворота по оси Z")]
        public float maxAngleZ = 30f;
    
        [Header("Скорость вращения")]
        [Tooltip("Скорость вращения по оси X (градусы в секунду)")]
        public float speedX = 20f;
    
        [Tooltip("Скорость вращения по оси Y (градусы в секунду)")]
        public float speedY = 30f;
    
        [Tooltip("Скорость вращения по оси Z (градусы в секунду)")]
        public float speedZ = 25f;
    
        [Header("Дополнительные настройки")]
        [Tooltip("Использовать локальные координаты (true) или мировые (false)")]
        public bool useLocalRotation = true;
    
        [Tooltip("Плавность движения (используется Lerp вместо прямого изменения)")]
        [Range(0f, 1f)]
        public float smoothness = 0f;
    
        // Внутренние переменные для отслеживания текущего состояния
        private Vector3 initialRotation;
        private float currentTimeX = 0f;
        private float currentTimeY = 0f;
        private float currentTimeZ = 0f;
    
        void Start()
        {
            // Сохраняем начальную ротацию объекта
            initialRotation = useLocalRotation ? transform.localEulerAngles : transform.eulerAngles;
        }
    
        void Update()
        {
            Vector3 targetRotation = initialRotation;
        
            // Вычисляем ping-pong для каждой оси
            if (rotateX)
            {
                currentTimeX += Time.deltaTime * speedX;
                float normalizedTime = Mathf.PingPong(currentTimeX, maxAngleX - minAngleX) / (maxAngleX - minAngleX);
                targetRotation.x = initialRotation.x + Mathf.Lerp(minAngleX, maxAngleX, normalizedTime);
            }
        
            if (rotateY)
            {
                currentTimeY += Time.deltaTime * speedY;
                float normalizedTime = Mathf.PingPong(currentTimeY, maxAngleY - minAngleY) / (maxAngleY - minAngleY);
                targetRotation.y = initialRotation.y + Mathf.Lerp(minAngleY, maxAngleY, normalizedTime);
            }
        
            if (rotateZ)
            {
                currentTimeZ += Time.deltaTime * speedZ;
                float normalizedTime = Mathf.PingPong(currentTimeZ, maxAngleZ - minAngleZ) / (maxAngleZ - minAngleZ);
                targetRotation.z = initialRotation.z + Mathf.Lerp(minAngleZ, maxAngleZ, normalizedTime);
            }
        
            // Применяем вращение с учетом плавности
            if (smoothness > 0f)
            {
                Vector3 currentRotation = useLocalRotation ? transform.localEulerAngles : transform.eulerAngles;
                targetRotation = Vector3.Lerp(currentRotation, targetRotation, smoothness * Time.deltaTime * 10f);
            }
        
            // Устанавливаем финальное вращение
            if (useLocalRotation)
            {
                transform.localEulerAngles = targetRotation;
            }
            else
            {
                transform.eulerAngles = targetRotation;
            }
        }
    
        /// <summary>
        /// Сброс вращения к начальному состоянию
        /// </summary>
        public void ResetRotation()
        {
            currentTimeX = 0f;
            currentTimeY = 0f;
            currentTimeZ = 0f;
        
            if (useLocalRotation)
            {
                transform.localEulerAngles = initialRotation;
            }
            else
            {
                transform.eulerAngles = initialRotation;
            }
        }
    
        /// <summary>
        /// Установить новую начальную ротацию
        /// </summary>
        public void SetInitialRotation(Vector3 rotation)
        {
            initialRotation = rotation;
            ResetRotation();
        }
    }
}
