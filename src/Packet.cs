using System;

namespace SocketFactory
{
    [Serializable]
    public abstract class Packet
    {
        internal enum PublicPacketType { Normal, File, FileComplete};
        internal PublicPacketType PacketType { get; set; }
        
        public Packet()
        {
            Type t = GetType();
            if (!t.IsDefined(typeof(SerializableAttribute), false))
            {
                throw new InvalidOperationException("Classes inheriting from SocketFactory.Base.Packet must be serializable.");
            }
        }
    }
}