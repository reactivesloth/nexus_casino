using System;
using System.Collections.Generic;
using System.Text;
using Code.Network.Stream.Data;
using UnityEngine;

namespace Code.Network.Stream
{
    public static class StreamFramesDataAccumulator
    {
        // Максимальное количество фреймов на один автомат
        private const int MaxFramesPerSlot = 3;

        private class FrameEntry
        {
            public List<StreamFrameChunkData> Chunks { get; set; }
            public long CreatedAtTick { get; set; }
        }

        // slotId -> (frameId -> FrameEntry)
        private static readonly Dictionary<int, Dictionary<int, FrameEntry>> Frames = new();

        public static void AddChunk(StreamFrameChunkData chunkData)
        {
            if (chunkData == null)
                throw new ArgumentNullException(nameof(chunkData));
    
            var slotId = chunkData.SlotId;
            var frameId = chunkData.FrameId;

            // Получаем или создаем словарь для этого слота
            if (!Frames.TryGetValue(slotId, out var slotDict))
            {
                slotDict = new Dictionary<int, FrameEntry>();
                Frames[slotId] = slotDict;
            }

            // Получаем или создаем запись для этого фрейма
            if (!slotDict.TryGetValue(frameId, out var frameEntry))
            {
                frameEntry = new FrameEntry
                {
                    Chunks = new List<StreamFrameChunkData>(chunkData.ChunkCount),
                    CreatedAtTick = DateTime.UtcNow.Ticks
                };
                slotDict[frameId] = frameEntry;

                CleanOldFramesForSlot(slotId);
            }

            // ← СОЗДАЁМ КОПИЮ чанка вместо добавления оригинала
            var chunkCopy = new StreamFrameChunkData
            {
                SlotId = chunkData.SlotId,
                StreamerId = chunkData.StreamerId,
                FrameId = chunkData.FrameId,
                ChunkIndex = chunkData.ChunkIndex,
                ChunkCount = chunkData.ChunkCount,
                Payload = chunkData.Payload  // Payload тоже скопируется (массив передаётся по ссылке, но для данного случая достаточно)
            };

            frameEntry.Chunks.Add(chunkCopy);
        }

        public static List<StreamFrameChunkData> GetChunks(int slotId, int frameId)
        {
            if (Frames.TryGetValue(slotId, out var slotDict) &&
                slotDict.TryGetValue(frameId, out var frameEntry))
            {
                return new List<StreamFrameChunkData>(frameEntry.Chunks);
            }

            return new List<StreamFrameChunkData>();
        }

        public static bool RemoveFrame(int slotId, int frameId)
        {
            if (!Frames.TryGetValue(slotId, out var slotDict))
                return false;

            var removed = slotDict.Remove(frameId);

            if (slotDict.Count == 0)
                Frames.Remove(slotId);

            return removed;
        }

        public static int GetChunkCount(int slotId, int frameId)
        {
            if (Frames.TryGetValue(slotId, out var slotDict) &&
                slotDict.TryGetValue(frameId, out var frameEntry))
            {
                return frameEntry.Chunks.Count;
            }

            return 0;
        }

        public static void Clear()
        {
            Frames.Clear();
        }

        // для одного slotId оставляем только MaxFramesPerSlot самых новых по CreatedAtTick
        private static void CleanOldFramesForSlot(int slotId)
        {
            if (!Frames.TryGetValue(slotId, out var slotDict))
                return;

            if (slotDict.Count <= MaxFramesPerSlot)
                return;

            var list = new List<(int frameId, long createdAt)>();
            foreach (var kvp in slotDict)
                list.Add((kvp.Key, kvp.Value.CreatedAtTick));

            list.Sort((a, b) => a.createdAt.CompareTo(b.createdAt)); // по возрастанию времени

            int toRemove = list.Count - MaxFramesPerSlot;
            for (int i = 0; i < toRemove; i++)
                slotDict.Remove(list[i].frameId);

            if (slotDict.Count == 0)
                Frames.Remove(slotId);
        }
    }
}