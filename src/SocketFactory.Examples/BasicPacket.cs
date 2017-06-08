using System;

namespace SocketFactory.Examples {

    [Serializable]
    public class BasicPacket : Packet {
        public BasicPacket(string data) {
            this.Data = data;
        }

        public BasicPacket() {

        }

        public string Data { set; get; }

        public override string ToString() {
            return String.IsNullOrWhiteSpace(Data) ? "No Data." : Data;
        }
    }
}
