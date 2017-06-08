using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketFactory
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class InternalPacket : Packet
    {
        public enum InternalPacketType { Ping, Error, RequestToShutdown }; // errors must not be encrypted
        public string ErrorMessage { get; set; }
        public new InternalPacketType PacketType { get; set; }
    }
}
