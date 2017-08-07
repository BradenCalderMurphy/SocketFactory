using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SocketFactory.Environment;
using System.Web.Script.Serialization;

namespace SocketFactory.Protocols.WebSocket {
    /*
 Notes
 Step 1: Client connects to the server                                                State: None
 Step 2: (OnReceiveData)Server receives a switching packet from the client            State: None -> PreSend
 Step 3: (OnSendPacket)Server sends it's interrupted switching packet to the client   State: Presend -> Send
 Step 4: (OnSendPacket/OnReceiveData)Server can now send/receive data                 State: Send -> Finished
     */
    public class WebSocketServerProtocol : IServerProtocol
    {
        private const int SETUP_PACKET_TIMEOUT_SECONDS = 15;
        private const int FIN_SIZE = 1;
        private const int KEY_SIZE = 4;
        private const int USHORT_BYTE = 126;
        private const int BUFFER_SIZE = 10000;

        private const byte OPCODE_BINARY = 0x2;
        private const byte OPCODE_CLOSE = 0x8;
        private const byte OPCODE_PING = 0x9;
        private const byte OPCODE_PONG = 0xA;
        private const byte OPCODE_TEXT = 0x1;

        private byte[] _receiveBuffer = new byte[BUFFER_SIZE];
        private byte[] _sendBuffer = new byte[BUFFER_SIZE];
        private List<byte> _addedBuffer = new List<byte>();
        private object _stateLock = new object();
        private enum ProtocolStates { None, PreSend, Sent, Finished }
        private ProtocolStates _state;
        private byte[] _clientSwitchingPacket;
        private DateTimeEnvironment _dtSetupPacketSent;

        public void OnSendPacket(BaseSpawn sender, Packet packet, out bool removePacket)
        {
            removePacket = false;
            int bufferSize = 0;
            lock (_stateLock) {
                switch (_state) {
                    case ProtocolStates.Finished:
                    case ProtocolStates.Sent:
                        if (!Encode(packet, out bufferSize)) return;
                        removePacket = true;
                        if (bufferSize == 0) return;
                        
                        sender.Stream.Write(_sendBuffer, 0, bufferSize);
                        break;
                    case ProtocolStates.PreSend:
                        bufferSize = _clientSwitchingPacket.Length;
                        sender.Stream.Write(_clientSwitchingPacket, 0, bufferSize);
                        _state = ProtocolStates.Sent;
                        _dtSetupPacketSent = DateTimeEnvironment.Now;
                        _clientSwitchingPacket = null;
                        break;
                    case ProtocolStates.None:
                        // do nothing
                        break;
                }
            }
        }

        public Packet OnReceiveData(BaseSpawn sender)
        {
            lock (_stateLock) {
                try {
                    if (!sender.Stream.DataAvailable) {
                        return null;
                    }

                    int length = Math.Min(_receiveBuffer.Length, sender.Socket.Available);
                    length = sender.Stream.Read(_receiveBuffer, 0, length);

                    for (int i = 0; i < length; ++i) {
                        _addedBuffer.Add(_receiveBuffer[i]);
                    }

                    switch (_state) {
                        case ProtocolStates.None:
                            byte[] response;
                            if (WebSocketServerProtocol.ValidResponse(Encoding.UTF8.GetString(_addedBuffer.ToArray()), out response)) {
                                _clientSwitchingPacket = response;
                                _state = ProtocolStates.PreSend;
                                _addedBuffer.Clear();
                            }
                            break;
                        case ProtocolStates.Finished:
                        case ProtocolStates.Sent:
                            if (_state == ProtocolStates.Sent) {
                                _state = ProtocolStates.Finished;
                            }
                            return Decode(sender);
                    }
                    return null;
                }
                finally {
                    if (_state == ProtocolStates.Sent) {
                        TimeSpanEnvironment ts = DateTimeEnvironment.Now.Subtract(_dtSetupPacketSent);
                        if (ts.TotalSeconds > SETUP_PACKET_TIMEOUT_SECONDS) {
                            throw new Exception("Web Server Packet Timeout.");
                        }
                    }
                }
            }
        }

        private static bool ValidResponse(string data, out byte[] response)
        {
            response = null;
            if (!new Regex("^GET").IsMatch(data)) return false;

            GroupCollection groups = new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups;
            if (groups == null || groups.Count <= 1) return false;

            const string END_DATA = "\r\n\r\n";
            if (!data.EndsWith(END_DATA)) return false;

            string key = groups[1].Value.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            response = Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + System.Environment.NewLine
                + "Connection: Upgrade" + System.Environment.NewLine
                + "Upgrade: websocket" + System.Environment.NewLine
                + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                    SHA1.Create().ComputeHash(
                        Encoding.UTF8.GetBytes(key)
                    )
                ) + System.Environment.NewLine
                + System.Environment.NewLine);
            return true;
        }

        public void OnDisconnect(BaseSpawn sender)
        {
            lock (_stateLock) {
                _addedBuffer.Clear();
                _state = ProtocolStates.None;
            }
        }

        public void OnConnect(BaseSpawn sender)
        {

        }

        private bool Encode(Packet packet, out int bufferSize)
        {
            bufferSize = 0;
            Array.Clear(_sendBuffer, 0, _sendBuffer.Length);
            byte finn = 128;    // 7
            byte rev1 = 0;      // 6
            byte rev2 = 0;      // 5
            byte rev3 = 0;      // 4
            byte opCode1 = 0;   // 3
            byte opCode2 = 0;   // 2
            byte opCode3 = 0;   // 1
            byte opCode4 = 0;   // 0

            byte myOpcode = 0;
            byte[] data = null;
            if (packet is InternalPacket &&
                (packet as InternalPacket).PacketType == InternalPacket.InternalPacketType.Ping) {
                myOpcode = OPCODE_PING;
            }
            else if (packet is BinaryPacket) {
                myOpcode = OPCODE_BINARY;
                data = (packet as BinaryPacket).Buffer;
            }
            else {
                myOpcode = OPCODE_TEXT;
                data = Encoding.UTF8.GetBytes(ObjectToJSON(packet));
            }
            opCode1 = (byte)(myOpcode & 8);
            opCode2 = (byte)(myOpcode & 4);
            opCode3 = (byte)(myOpcode & 2);
            opCode4 = (byte)(myOpcode & 1);

            byte bytesPacketSize = 1;
            byte byte1 = 0;
            int packetLen = 0;
            if (data != null) {
                packetLen = data.Length;
            }

            if (packetLen <= 125) {
                byte1 = (byte)packetLen;
            }
            else {
                byte1 = USHORT_BYTE;
                bytesPacketSize += 2;
            }

            bufferSize = FIN_SIZE +
                  bytesPacketSize +
                  packetLen;

            int index = 0;
            _sendBuffer[index++] = (byte)(finn + rev1 + rev2 + rev3 +
                                opCode1 + opCode2 + opCode3 + opCode4);
            _sendBuffer[index++] = byte1;
            switch (byte1) {
                case USHORT_BYTE:
                    _sendBuffer[index++] = (byte)(packetLen >> 8);
                    _sendBuffer[index++] = (byte)(packetLen & 255);
                    break;
            }

            if (data != null) {
                foreach (byte b in data) {
                    _sendBuffer[index++] = b;
                }
            }
            return true;
        }

        private static string ObjectToJSON(object toSerialize) {
            return new JavaScriptSerializer().Serialize(toSerialize);
        }

        private Packet Decode(BaseSpawn sender)
        {
            int index = 0;
            byte byte1 = _addedBuffer[index++];
            bool wholemessage = (byte1 & 128) > 0;
            byte opCode = (byte)(byte1 - (byte1 & 128));
            switch (opCode) {
                case OPCODE_TEXT:
                case OPCODE_BINARY:
                case OPCODE_PING:
                case OPCODE_PONG:
                    break;
                case OPCODE_CLOSE:
                    _addedBuffer.Clear();
                    return null; // I can improve and make a more graceful way of closing the connection
                default:
                    _addedBuffer.Clear();
                    sender.GeneralExceptionLog("Opcode not supported: " + opCode);
                    return null;
            }
            byte byte2 = _addedBuffer[index++];
            byte2 = (byte)(byte2 - (byte2 & 128));
            int length = 0;
            if (byte2 <= 125) {
                //1 byte
                length = byte2;
            }
            else if (byte2 == 126) {
                // 3 bytes 
                length += _addedBuffer[index++] << 8;
                length += _addedBuffer[index++] & 255;
            }
            else {
                throw new Exception("Packet is too long: " + byte1);
            }

            Byte[] key = new Byte[KEY_SIZE];
            key[0] = _addedBuffer[index++];
            key[1] = _addedBuffer[index++];
            key[2] = _addedBuffer[index++];
            key[3] = _addedBuffer[index++];

            int combinedLength = length + index;
            if (_addedBuffer.Count < combinedLength) return null;

            Byte[] decoded = new Byte[length];
            for (int i = 0; i < decoded.Length; i++) {
                decoded[i] = (Byte)(_addedBuffer[index + i] ^ key[i % 4]);
            }

            try {
                switch (opCode) {
                    case OPCODE_TEXT:
                        return new TextPacket(Encoding.UTF8.GetString(decoded));
                    case OPCODE_BINARY:
                        return new BinaryPacket(decoded);
                    case OPCODE_PING:
                    case OPCODE_PONG:
                        return new InternalPacket() { PacketType = InternalPacket.InternalPacketType.Ping };
                }
                return null;
            }
            finally {
                _addedBuffer.RemoveRange(0, combinedLength);
            }
        }

    }
}
