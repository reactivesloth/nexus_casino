using LiteNetLib.Utils;

namespace Code.Network.Stream.Data
{
    public class StreamFrameChunkData: INetSerializable
    {
        public const int HeaderSize = 32;
        
        public int SlotId { get; set; }

        public int FrameId { get; set; }
        
        public ushort ChunkIndex { get; set; }
        public ushort ChunkCount { get; set; }
        public byte[] Payload { get; set; }
        
        
        public void Serialize(NetDataWriter writer)
        {
            writer.Put(SlotId);
            writer.Put(FrameId);
            writer.Put(ChunkIndex);
            writer.Put(ChunkCount);
            writer.PutBytesWithLength(Payload);
        }

        public void Deserialize(NetDataReader reader)
        {
            SlotId = reader.GetInt();
            FrameId = reader.GetInt();
            ChunkIndex = reader.GetUShort();
            ChunkCount = reader.GetUShort();
            Payload = reader.GetBytesWithLength();
        }

        public override string ToString()
        {
            return $"SlotId={SlotId}, FrameId={FrameId}, " +
                   $"ChunkIndex={ChunkIndex}, ChunkCount={ChunkCount}, PayloadLength={Payload?.Length ?? 0}";
        }
    }
}