using System;

namespace SocketFactory.Protocols.WebSocket {
    [Serializable]
    public class TextPacket : Packet
    {
        public TextPacket(string message)
        {
            this.Message = message;
        }

        public string Message { set; get; }
    }
}
