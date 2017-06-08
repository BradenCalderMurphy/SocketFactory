using System;
using System.Net;
using System.Net.Sockets;
using SocketFactory.Environment;

namespace SocketFactory
{
    public class ClientSpawn : BaseSpawn
    {
        public const int CONNECT_TIME_DELAY = 30; // seconds
        public const int DEFAULT_SERVER_PORT = 12859;

        private DateTimeEnvironment _dtLastConnectAttempt;
        private object _connectingLock = new object();
        private bool _connecting = false;
        private bool Connecting
        {
            set
            {
                lock (_connectingLock)
                {
                    _connecting = value;
                }
            }
            get
            {
                lock (_connectingLock)
                {
                    return _connecting;
                }
            }
        }

        private object _serverIPLock = new object();
        private IPAddress _serverIP = null;
        public IPAddress ServerIP
        {
            set
            {
                lock (_serverIPLock)
                {
                    _serverIP = value;
                }
            }
            get
            {
                lock (_serverIPLock)
                {
                    return _serverIP;
                }
            }
        }

        private object _serverPortLock = new object();
        private int _serverPort = DEFAULT_SERVER_PORT;
        public int ServerPort
        {
            set
            {
                lock (_serverPortLock)
                {
                    _serverPort = value;
                }
            }
            get
            {
                lock (_serverPortLock)
                {
                    return _serverPort;
                }
            }
        }

        private object _passwordLock = new object();
        private string _password;
        public string Password
        {
            set
            {
                lock (_passwordLock)
                {
                    _password = value;
                }
            }
            get
            {
                lock (_passwordLock)
                {
                    return _password;
                }
            }
        }

        public ClientSpawn(IBaseSpawnHandler handler)
            : this(handler, null)
        {

        }

        internal ClientSpawn(IBaseSpawnHandler handler, Type protocolType)
            : base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), handler, protocolType)
        {
            _dtLastConnectAttempt = DateTimeEnvironment.Now.AddSeconds(-CONNECT_TIME_DELAY);
        }

        protected override void BaseStart()
        {
            // do nothing
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                GetSocket().EndConnect(ar);
                SetConnected(this.Password);
            }
            catch (Exception ex)
            {
                GeneralExceptionLog("ConnectCallback: " + ex.Message);
            }
            finally
            {
                this.Connecting = false;
            }
        }

        #region Inherited Abstract Methods
        protected override void Running()
        {
            if (Connecting) return;
            if (!Connected)
            {
                TimeSpanEnvironment span = DateTimeEnvironment.Now.Subtract(_dtLastConnectAttempt);
                if (span.TotalSeconds < CONNECT_TIME_DELAY) return;

                _dtLastConnectAttempt = DateTimeEnvironment.Now;

                try
                {
                    Connecting = true;
                    IPAddress ip = this.ServerIP;
                    int port = this.ServerPort;
                    GetSocket().BeginConnect(new IPEndPoint(ip, port), ConnectCallback, null);
                }
                catch (Exception ex)
                {
                    Connecting = false;
                    GeneralExceptionLog("ClientSpawn.StartClient: " + ex.Message + ", " + this.ServerIP);
                }
            }
        }

        protected override void StopBase()
        {

        }
        #endregion
    }
}
