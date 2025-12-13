using System;
using System.Collections.Generic;
using Code.Network.Stream.Data;
using UnityEngine;

namespace Code.Network.Stream
{
    public static class StreamFramesDataAccumulator
    {
        // Максимальное количество фреймов на один автомат
        private const int MaxFramesPerSlot = 3;

        // Данные о фрейме: список чанков + время добавления
        private class FrameEntry
        {
            public List<StreamFrameChunkData> Chunks { get; set; }
            public long CreatedAtTick { get; set; }
        }

        // Статичный словарь для быстрого доступа: (slotId, frameId) -> FrameEntry
        private static readonly Dictionary<(int slotId, int frameId), FrameEntry> Frames = new();

        public static void AddChunk(StreamFrameChunkData chunkData)
        {
            if (chunkData == null)
                throw new ArgumentNullException(nameof(chunkData));

            var key = (chunkData.SlotId, chunkData.FrameId);

            // Получаем или создаем запись для этого фрейма
            if (!Frames.TryGetValue(key, out var frameEntry))
            {
                frameEntry = new FrameEntry
                {
                    Chunks = new List<StreamFrameChunkData>(chunkData.ChunkCount),
                    CreatedAtTick = DateTime.UtcNow.Ticks
                };
                Frames[key] = frameEntry;

                // Проверяем и удаляем старые фреймы если их слишком много
                CleanOldFramesForSlot(chunkData.SlotId);
            }

            frameEntry.Chunks.Add(chunkData);
        }

        public static List<StreamFrameChunkData> GetChunks(int slotId, int frameId)
        {
            var key = (slotId, frameId);

            if (Frames.TryGetValue(key, out var frameEntry))
            {
                return new List<StreamFrameChunkData>(frameEntry.Chunks);
            }

            return new List<StreamFrameChunkData>();
        }

        // Удалить собранный фрейм из памяти
        public static bool RemoveFrame(int slotId, int frameId)
        {
            var key = (slotId, frameId);
            return Frames.Remove(key);
        }

        // Получить количество чанков для фрейма
        public static int GetChunkCount(int slotId, int frameId)
        {
            var key = (slotId, frameId);
            return Frames.TryGetValue(key, out var frameEntry) ? frameEntry.Chunks.Count : 0;
        }

        // Очистить все данные
        public static void Clear()
        {
            Frames.Clear();
        }

        // Удалить старые фреймы для конкретного автомата если их больше MaxFramesPerSlot
        private static void CleanOldFramesForSlot(int slotId)
        {
            var slotFrames = new List<((int slotId, int frameId) key, long createdAtTick)>();

            // Собираем все фреймы этого автомата с временем создания
            foreach (var kvp in Frames)
            {
                if (kvp.Key.slotId == slotId)
                {
                    slotFrames.Add((kvp.Key, kvp.Value.CreatedAtTick));
                }
            }

            // Если фреймов больше чем нужно, удаляем самые старые по времени добавления
            if (slotFrames.Count > MaxFramesPerSlot)
            {
                slotFrames.Sort((a, b) => a.createdAtTick.CompareTo(b.createdAtTick));

                int toRemove = slotFrames.Count - MaxFramesPerSlot;
                for (int i = 0; i < toRemove; i++)
                {
                    Frames.Remove(slotFrames[i].key);
                }
            }
        }
    }
}