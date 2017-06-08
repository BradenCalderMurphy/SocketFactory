using System;

namespace SocketFactory.Protocols.WebSocket {
    [Serializable]
    public class BinaryPacket : Packet
    {
        public BinaryPacket(byte[] buffer)
        {
            this.Buffer = buffer;
        }

        public byte[] Buffer { set; get; }
    }
}
