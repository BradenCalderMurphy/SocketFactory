using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketFactory.Examples {

    public class FactoryProgram {

        private MyServer _server;
        private MyClient _client;

        public static void Run() {
            new FactoryProgram().InternalRun();
        }

        private void InternalRun() {

            if (this.Start()) {
                Console.WriteLine("Press any key to close the connections and exit.");
                Console.ReadLine();
                _client?.Stop();
                _server?.Stop();
            }
        }

        private bool Start() {

            Console.WriteLine("Start the server & client");
            Console.WriteLine("***********************************");
            Console.WriteLine("Please enter 1 - 4:");
            Console.WriteLine("1: Unencrypted Server");
            Console.WriteLine("2: Encrypted Server");
            Console.WriteLine("3: Unencrypted Web Server");
            Console.WriteLine("4: Exit");

            MyServer.ServerTypes t;

            string a = Console.ReadLine();
            switch(a) { 
                case "1":
                    t = MyServer.ServerTypes.Normal;
                    break;
                case "2":
                    t = MyServer.ServerTypes.NormalEncrypted;
                    break;
                case "3":
                    t = MyServer.ServerTypes.Web;
                    break;
                case "4":
                    return false;
                default:
                    Console.WriteLine("Incorrect input.");
                    return Start();
            }
            Console.WriteLine("Starting Server...");
            _server = new MyServer(t);
            switch (t) {
                case MyServer.ServerTypes.Normal:
                    Console.WriteLine("Starting Client...");
                    _client = new MyClient(MyClient.ClientTypes.Normal);
                    break;
                case MyServer.ServerTypes.NormalEncrypted:
                    Console.WriteLine("Starting Client...");
                    _client = new MyClient(MyClient.ClientTypes.NormalEncrypted);
                    break;
                case MyServer.ServerTypes.Web:
                    Console.WriteLine("Please open up 'index.html' to start the web client.");
                    break;
            }
            return true;
        }
    }
}
