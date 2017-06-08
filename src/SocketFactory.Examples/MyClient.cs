using SocketFactory;
using System;
using System.Net;

namespace SocketFactory.Examples {

    public class MyClient : IBaseSpawnHandler {

        private const int SEND_TIME_SECONDS = 2;

        public enum ClientTypes { Normal, NormalEncrypted };

        private ClientSpawn _client;
        private DateTime _dtSend;

        public MyClient(ClientTypes clientType) {
            _client = new ClientSpawn(this);
            _client.ServerPort = Globals.PORT;
            _client.ServerIP = IPAddress.Parse("127.0.0.1");

            switch (clientType) {
                case ClientTypes.NormalEncrypted:
                    _client.Password = "password1234";
                    break;
            }
            _client.Start();
        }

        public void Stop() {
            _client.Stop();
        }

        public void OnCompleteStreamRead(BaseSpawn sender, StreamPacket packet) {
        
        }

        public void OnConnect(BaseSpawn sender) {
         
        }

        public void OnDisconnect(BaseSpawn sender, string message) {
         
        }

        public void OnExceptionLog(object sender, string message) {
            Console.WriteLine("MyClient.EXCEPTION: " + message);
        }

        public void OnRead(BaseSpawn sender, Packet packet) {
            Console.WriteLine(packet.ToString());
        }

        public void OnReadStream(BaseSpawn sender, byte[] readBytes, StreamPacket packet) {
           
        }

        public void OnStart(BaseSpawn sender) {

        }

        public void WhileConnected(BaseSpawn sender) {
            TimeSpan ts = DateTime.Now.Subtract(_dtSend);
            if(ts.TotalSeconds >= SEND_TIME_SECONDS) {
                _dtSend = DateTime.Now;
                _client.EnqueueObject(new BasicPacket("This is a message from the client at " + DateTime.Now));
            }
        }
    }
}
