using System;
using System.Collections.Generic;

namespace SocketFactory.Encrypt
{
    [Serializable]
    public class DataTransport
    {
        public List<byte> Data { set; get; } // encrypted

        public DataTransport()
        {
            Data = new List<byte>();
        }

        public DataTransport(List<byte> data)
        {
            Data = data;
        }
    }
}
