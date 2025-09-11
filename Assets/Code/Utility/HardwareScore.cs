using UnityEngine;

namespace Code.Utility
{
    public static class HardwareScore
    {
        // Эталонные значения — можешь менять под свой проект
        private const int cpuFreqRef = 3000;     // МГц
        private const int cpuCoresRef = 8;
        private const int ramRefMB = 16000;      // МБ

        // Веса
        private const float wCPU = 0.7f;
        private const float wRAM = 0.3f;

        /// <summary>
        /// Возвращает итоговый "балл мощности" в диапазоне 0-1000
        /// </summary>
        public static int GetScore()
        {
            int cpuFreq = SystemInfo.processorFrequency;    // МГц
            int cpuCores = SystemInfo.processorCount;
            int ramMB = SystemInfo.systemMemorySize;        // МБ

            // Если какие-то значения не определились — подставляем эталон
            if (cpuFreq <= 0) cpuFreq = cpuFreqRef;
            if (cpuCores <= 0) cpuCores = cpuCoresRef;
            if (ramMB <= 0) ramMB = ramRefMB;

            // CPU оценка
            float cpuScore = ((float)cpuFreq / cpuFreqRef) * ((float)cpuCores / cpuCoresRef);

            // RAM оценка
            float ramScore = (float)ramMB / ramRefMB;

            // Ограничим максимум
            cpuScore = Mathf.Min(cpuScore, 1f);
            ramScore = Mathf.Min(ramScore, 1f);

            // Итоговый балл
            float combined = cpuScore * wCPU + ramScore * wRAM;
            return Mathf.RoundToInt(combined * 1000f);
        }
    }
}