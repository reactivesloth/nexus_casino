using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Code.Network.Stream.Data;
using UnityEngine;

namespace Code.Network.Stream.Utility
{
    public static class FrameBuilder
    {
        public const int MaxPayloadSize = 768;

        public static StreamFrameChunkData[] GetFrameChunks(StreamFrameData frameData)
        {
            if (frameData == null)
                throw new ArgumentNullException(nameof(frameData));

            if (frameData.Data == null || frameData.Data.Length == 0)
                return Array.Empty<StreamFrameChunkData>();

            // Вычисляем количество чанков
            int dataLength = frameData.Data.Length;
            int chunkCount = (dataLength + MaxPayloadSize - 1) / MaxPayloadSize; // Округление вверх

            var chunks = new StreamFrameChunkData[chunkCount];

            for (ushort i = 0; i < chunkCount; i++)
            {
                var chunkIndex = i;
                int startIndex = i * MaxPayloadSize;
                int remainingBytes = dataLength - startIndex;
                int payloadSize = Math.Min(remainingBytes, MaxPayloadSize);

                var payload = new byte[payloadSize];
                Array.Copy(frameData.Data, startIndex, payload, 0, payloadSize);

                chunks[i] = new StreamFrameChunkData
                {
                    SlotId = frameData.SlotId,
                    StreamerId = frameData.StreamerId,
                    FrameId = frameData.FrameId,
                    ChunkIndex = chunkIndex,
                    ChunkCount = (ushort)chunkCount,
                    Payload = payload
                };
            }

            return chunks;
        }

        public static bool TryGetFullFrame(List<StreamFrameChunkData> chunks, out StreamFrameData frameData)
        {
            frameData = null;

            // Проверяем что список не пуст
            if (chunks == null || chunks.Count == 0)
                return false;

            // Все чанки должны быть от одного фрейма
            int frameId = chunks[0].FrameId;
            int streamerId = chunks[0].StreamerId;
            int slotId = chunks[0].SlotId;
            ushort expectedChunkCount = chunks[0].ChunkCount;

            // Проверяем что у нас есть ровно нужное количество чанков
            if (chunks.Count != expectedChunkCount)
            {
                //Debug.LogWarning($"[StreamingLiteNetLibPeer] chunks.Count != expectedChunkCount");
                return false;
            }

            // Проверяем что все чанки от одного фрейма и индексы правильные
            var seenIndices = new bool[expectedChunkCount];

            foreach (var chunk in chunks)
            {
                // Проверяем консистентность метаданных
                if (chunk.FrameId != frameId ||
                    chunk.StreamerId != streamerId ||
                    chunk.SlotId != slotId ||
                    chunk.ChunkCount != expectedChunkCount)
                {
                    Debug.LogWarning($"[StreamingLiteNetLibPeer] data error");
                    return false;
                }

                // Проверяем валидность индекса
                if (chunk.ChunkIndex < 0 || chunk.ChunkIndex >= expectedChunkCount)
                {
                    Debug.LogWarning($"[StreamingLiteNetLibPeer] index error");
                    return false;
                }

                /*// Проверяем дубликаты
                if (seenIndices[chunk.ChunkIndex])
                {
                    Debug.LogWarning($"[StreamingLiteNetLibPeer] dublicate error");
                    return false;
                }*/

                seenIndices[chunk.ChunkIndex] = true;

                // Проверяем что payload не null
                if (chunk.Payload == null)
                {
                    Debug.LogWarning($"[StreamingLiteNetLibPeer] empty payload");
                    return false;
                }
            }

            // Вычисляем размер итогового фрейма
            int totalSize = chunks.Sum(c => c.Payload.Length);
            var data = new byte[totalSize];

            // Собираем данные в правильном порядке
            int offset = 0;
            var indexesStr = new StringBuilder();
            chunks.ForEach(c => indexesStr.Append($"{c.ChunkIndex}, "));
            Debug.Log($"[FrameBuilder] Frames indexes: {indexesStr}");
            for (ushort i = 0; i < expectedChunkCount; i++)
            {
                Debug.Log($"[FrameBuilder] chunk {i} of {expectedChunkCount}");
                var chunk = chunks.First(c => c.ChunkIndex == i);
                Array.Copy(chunk.Payload, 0, data, offset, chunk.Payload.Length);
                offset += chunk.Payload.Length;
            }

            // Формируем результат
            frameData = new StreamFrameData
            {
                FrameId = frameId,
                StreamerId = streamerId,
                SlotId = slotId,
                Data = data
            };

            return true;
        }
    }
}