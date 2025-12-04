namespace Code.Network.Stream.Data
{
    public class StreamFrameData
    {
        public int StreamerId { get; set; }
        public int SlotId { get; set; }
        public byte[] Data { get; set; }
    }
}