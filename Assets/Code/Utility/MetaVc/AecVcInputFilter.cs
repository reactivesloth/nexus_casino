using System;
using UnityEngine;
using MetaVoiceChat.Input;

namespace Code.Utility.MetaVc
{
    public class AecVcInputFilter : VcInputFilter
    {
        [Header("AEC Параметры")]
        [Range(0.1f, 2f)]
        public float echoSensitivity = 0.8f;
        
        [Range(0f, 1f)]
        public float suppressionStrength = 0.7f;
        
        [Range(10, 100)]
        public int analysisWindowMs = 20;
        
        // ✅ ИСПРАВЛЕННЫЕ буферы (фиксированный размер)
        private float[] farEndRingBuffer = new float[480 * 5]; // 100ms при 48kHz
        private int farEndWritePos = 0;
        private int farEndSampleCount = 0;
        
        private float farEndRms = 0f;
        private float adaptiveThreshold = 0.01f;
        
        private const float DecayFactor = 0.95f;
        private const int RingBufferSize = 480 * 5; // 5 фреймов по 480 сэмплов

        private void OnEnable()
        {
            farEndRms = 0f;
            adaptiveThreshold = 0.01f;
            farEndWritePos = 0;
            farEndSampleCount = 0;
            
            // ✅ НЕ используем InvokeRepeating - только FixedUpdate для синхронизации
            // Захват будет в Filter() по требованию
        }

        private void OnDisable()
        {
            Array.Clear(farEndRingBuffer, 0, RingBufferSize);
        }

        private void UpdateFarEndCapture()
        {
            // ✅ Захватываем только 1 фрейм (480 сэмплов) - НЕ 256!
            float[] farEndSamples = new float[480];
            AudioListener.GetOutputData(farEndSamples, 0);
            
            if (farEndSamples.Length > 0)
            {
                // ✅ Копируем только доступные сэмплы
                int toCopy = Mathf.Min(farEndSamples.Length, 480);
                for (int i = 0; i < toCopy; i++)
                {
                    farEndRingBuffer[farEndWritePos] = farEndSamples[i];
                    farEndWritePos = (farEndWritePos + 1) % RingBufferSize;
                    if (farEndSampleCount < RingBufferSize)
                        farEndSampleCount++;
                }
            }
        }

        protected override void Filter(int index, ref float[] micSamples)
        {
            if (micSamples == null || micSamples.Length == 0)
                return;

            // ✅ Захватываем far-end ТОЛЬКО когда нужен mic
            UpdateFarEndCapture();
            
            // Анализируем микрофон
            float micRms = CalculateRms(micSamples);
            
            // Обновляем far-end RMS (ОПТИМИЗИРОВАНО)
            UpdateFarEndRmsFast();
            
            // Adaptive threshold
            adaptiveThreshold = Mathf.Lerp(adaptiveThreshold, 
                farEndRms * echoSensitivity, 0.2f);
            
            // Детекция эхо
            bool echoDetected = micRms < adaptiveThreshold * 1.2f && 
                               farEndRms > 0.005f;
            
            if (echoDetected)
            {
                ApplyEchoSuppression(ref micSamples, suppressionStrength);
            }
            else if (farEndRms > 0.01f)
            {
                // Легкое подавление фона
                ApplyEchoSuppression(ref micSamples, suppressionStrength * 0.2f);
            }
        }

        private float CalculateRms(float[] samples)
        {
            float sum = 0f;
            int len = samples.Length;
            if (len == 0) return 0f;
            
            // ✅ Векторизованный расчет (быстрее)
            for (int i = 0; i < len; i++)
            {
                float s = samples[i];
                sum += s * s;
            }
            return Mathf.Sqrt(sum / len);
        }

        private void UpdateFarEndRmsFast()
        {
            if (farEndSampleCount == 0) 
            {
                farEndRms = 0f;
                return;
            }
            
            // ✅ Быстрый RMS только последних 2400 сэмплов (~50ms)
            float sum = 0f;
            int samplesToCheck = Mathf.Min(2400, farEndSampleCount);
            int startPos = (farEndWritePos - samplesToCheck + RingBufferSize) % RingBufferSize;
            
            for (int i = 0; i < samplesToCheck; i++)
            {
                int idx = (startPos + i) % RingBufferSize;
                float s = farEndRingBuffer[idx];
                sum += s * s;
            }
            
            farEndRms = Mathf.Sqrt(sum / samplesToCheck) * DecayFactor;
        }

        private void ApplyEchoSuppression(ref float[] samples, float strength)
        {
            // ✅ УБРАЛИ тяжелый поиск maxSample
            for (int i = 0; i < samples.Length; i++)
            {
                // Простое затухание
                samples[i] *= (1f - strength);
                
                // Жесткое обнуление слабого эха
                if (Mathf.Abs(samples[i]) < adaptiveThreshold * 1.5f)
                    samples[i] *= 0.05f; // Почти ноль
            }
        }

        // ✅ Debug без UNITY_EDITOR
        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            
            UnityEngine.GUI.Label(new Rect(10, 10, 300, 50),
                $"AEC Debug:\nFarEnd RMS: {farEndRms:F4}\nThresh: {adaptiveThreshold:F4}\nSamples: {farEndSampleCount}");
        }
    }
}
