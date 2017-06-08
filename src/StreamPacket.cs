using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketFactory
{
    [Serializable]
    public class StreamPacket : Packet
    {
        [NonSerialized]
        private Stream _streamToSend = null;
        public Stream StreamToSend
        {
            get { return _streamToSend; }
            set { _streamToSend = value; }
        } 

        public long StreamLength { get; }
        public Type StreamType { get; set; }

        public StreamPacket(Stream stream)
        {
            StreamToSend = stream;
            StreamType = stream.GetType();
            StreamLength = stream.Length;
        }
    }
}
