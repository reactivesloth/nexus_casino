using UnityEngine;
using System;
using MetaVoiceChat.Input;

namespace Code.Utility.MetaVc
{
    /// <summary>
    /// VAD (Voice Activity Detection) фильтр для голосового чата.
    /// Обнаруживает речь и обнуляет тишину.
    /// </summary>
    public class VadVcInputFilter : VcInputFilter
    {
        [Header("VAD Параметры")]
        [Range(0.001f, 0.1f)]
        [Tooltip("Порог мощности для детекции речи")]
        public float speechThreshold = 0.02f;
        
        [Range(0.001f, 0.05f)]
        [Tooltip("Порог для детекции тишины")]
        public float silenceThreshold = 0.005f;
        
        [Range(1, 20)]
        [Tooltip("Количество фреймов для подтверждения речи")]
        public int speechConfirmFrames = 3;
        
        [Range(1, 10)]
        [Tooltip("Количество фреймов для подтверждения тишины")]
        public int silenceConfirmFrames = 2;
        
        [Tooltip("Усиление речи (0-2.0)")]
        [Range(0f, 2f)]
        public float speechGain = 1.2f;

        private const int FrameSize = 480; // 20ms при 48kHz
        private float[] frameBuffer = new float[FrameSize];
        
        private int speechFrameCount = 0;
        private int silenceFrameCount = 0;
        private bool isSpeaking = false;
        
        private float rmsCache = 0f;
        private int frameIndex = 0;

        protected override void Filter(int index, ref float[] samples)
        {
            if (samples == null || samples.Length == 0 || samples.Length % FrameSize != 0)
                return;

            int frameCount = samples.Length / FrameSize;
            
            for (int i = 0; i < frameCount; i++)
            {
                // Копируем фрейм
                Array.Copy(samples, i * FrameSize, frameBuffer, 0, FrameSize);
                
                // Вычисляем RMS (Root Mean Square) для детекции активности
                float rms = CalculateRms(frameBuffer, FrameSize);
                
                // Проверяем состояние VAD
                bool speechDetected = UpdateVadState(rms);
                
                if (speechDetected)
                {
                    // Усиливаем речь
                    ApplyGain(frameBuffer, FrameSize, speechGain);
                }
                else
                {
                    // Обнуляем тишину
                    Array.Clear(frameBuffer, 0, FrameSize);
                }
                
                // Копируем обратно
                Array.Copy(frameBuffer, 0, samples, i * FrameSize, FrameSize);
            }
        }
        
        private float CalculateRms(float[] data, int length)
        {
            float sum = 0f;
            for (int i = 0; i < length; i++)
            {
                float sample = data[i];
                sum += sample * sample;
            }
            return Mathf.Sqrt(sum / length);
        }
        
        private bool UpdateVadState(float rms)
        {
            // Адаптивный порог на основе недавней активности
            float adaptiveThreshold = Mathf.Lerp(silenceThreshold, speechThreshold, 
                Mathf.Clamp01(speechFrameCount / (float)speechConfirmFrames));
            
            if (rms > adaptiveThreshold)
            {
                speechFrameCount++;
                silenceFrameCount = 0;
                
                if (speechFrameCount >= speechConfirmFrames)
                {
                    isSpeaking = true;
                }
            }
            else
            {
                silenceFrameCount++;
                speechFrameCount = 0;
                
                if (silenceFrameCount >= silenceConfirmFrames)
                {
                    isSpeaking = false;
                }
            }
            
            return isSpeaking;
        }
        
        private void ApplyGain(float[] data, int length, float gain)
        {
            for (int i = 0; i < length; i++)
            {
                data[i] *= gain;
                // Защита от клиппинга
                if (data[i] > 1f) data[i] = 1f;
                if (data[i] < -1f) data[i] = -1f;
            }
        }
    }
}
