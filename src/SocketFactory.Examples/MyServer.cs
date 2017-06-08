using System;
using SocketFactory;
using SocketFactory.Protocols.WebSocket;
using System.Net;

namespace SocketFactory.Examples {

    public class MyServer : IBaseSpawnHandler {

        private const int SEND_TIME_SECONDS = 3;

        public enum ServerTypes { Normal, Web, NormalEncrypted }; 

        private PrimeServer _server;
        private DateTime _dtSend;

        public MyServer(ServerTypes serverType) {

            switch (serverType) {
                case ServerTypes.Normal:
                case ServerTypes.NormalEncrypted:
                    _server = new PrimeServer(this);
                    break;
                case ServerTypes.Web:
                    _server = new PrimeServer<WebSocketServerProtocol>(this);
                    break;
            }

            _server.PortLocalEndPoint = Globals.PORT;

            switch (serverType) {
                case ServerTypes.NormalEncrypted:
                    _server.Start(new AllowedUser(IPAddress.Parse("127.0.0.1"), "password1234"));
                    break;
                default:
                    _server.Start();
                    break;

            }
        }

        public void OnCompleteStreamRead(BaseSpawn sender, StreamPacket packet) {

        }

        public void Stop() {
            _server.Stop();
        }

        public void OnConnect(BaseSpawn sender) {

        }

        public void OnDisconnect(BaseSpawn sender, string message) {

        }

        public void OnExceptionLog(object sender, string message) {
            Console.WriteLine("MyServer.EXCEPTION: " + message);
        }

        public void OnRead(BaseSpawn sender, Packet packet) {
            if (packet is TextPacket) {
                Console.WriteLine((packet as TextPacket).Message);
            }
            else {
                Console.WriteLine(packet.ToString());
            }
        }

        public void OnReadStream(BaseSpawn sender, byte[] readBytes, StreamPacket packet) {

        }

        public void OnStart(BaseSpawn sender) {

        }

        public void WhileConnected(BaseSpawn sender) {
            TimeSpan ts = DateTime.Now.Subtract(_dtSend);
            if (ts.TotalSeconds >= SEND_TIME_SECONDS) {
                _dtSend = DateTime.Now;
                _server.Broadcast(new BasicPacket("This is a message from the server at " + DateTime.Now));
            }
        }
    }
}
