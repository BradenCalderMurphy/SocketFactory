using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace SocketFactory {

    public class PrimeServer {
        public const int DEFAULT_SERVER_PORT = 12859;
        public const int LISTEN_EXCEPTION_MAX = 6;

        private IPAddress _ipLocalEndPoint = IPAddress.Any;
        private bool _hasStarted;
        private int _listeningException;
        private bool _socketDisposed = false;
        private Socket _serverSocket = null;
        private object _serverSocketLock = new object();
        private readonly IBaseSpawnHandler _handler;
        private readonly Type _protocolType;
        private object _clientListLock = new object();
        private List<ServerSpawn> _clientList = new List<ServerSpawn>();
        private object _usersLock = new object();
        private Dictionary<IPAddress, AllowedUser> _users = new Dictionary<IPAddress, AllowedUser>();

        public IPAddress IPLocalEndPoint {
            set {
                if (value == null) {
                    value = IPAddress.Any;
                }
                _ipLocalEndPoint = value;
            }
            get {
                return _ipLocalEndPoint;
            }
        }

        public int PortLocalEndPoint { set; get; } = DEFAULT_SERVER_PORT;

        public PrimeServer()
            : this(null) {

        }

        public PrimeServer(IBaseSpawnHandler handler)
            : this(handler, null) {

        }

        internal PrimeServer(IBaseSpawnHandler handler, Type protocolType) {
            _handler = handler;
            _protocolType = protocolType;
        }


        public void Start() {
            this.Start(null);
        }

        /// <summary>
        /// Start the server with a list of valid client IP's and passwords.
        /// </summary>
        /// <param name="clients">Dictionary containing client's IP's and passwords, with the IP
        /// used as the key, and the password used as the value.</param>
        public void Start(params AllowedUser[] users) {
            if (_hasStarted) return;
            _hasStarted = true;
            lock (_usersLock) {
                _users.Clear();
                if (users != null) {
                    foreach (AllowedUser user in users) {
                        if (_users.ContainsKey(user.IP)) {
                            GeneralExceptionLog($"There are users with the same IP Address: " + user.IP);
                            continue;
                        }
                        _users.Add(user.IP, user);
                    }
                }
            }

            List<IPAddress> lst = GetValidIPAddress();
            if ((!lst.Contains(this.IPLocalEndPoint)) && (!(this.IPLocalEndPoint.Equals(IPAddress.Any)))) {
                GeneralExceptionLog($"Cannot start the server as the IP Address - {this.IPLocalEndPoint} is not assigned to one of it's network cards.");
                return;
            }

            try {
                //  Set up the local end point to listen on the server's IP address
                IPEndPoint localEndPoint = new IPEndPoint(this.IPLocalEndPoint, this.PortLocalEndPoint);

                lock (_serverSocketLock) {
                    _socketDisposed = false;
                    _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _serverSocket.Bind(localEndPoint);
                }
                StartListening();
            }
            catch (Exception ex) {
                GeneralExceptionLog($"Cannot start the server: " + ex.Message);
            }
        }

        /// <summary>
        /// Listens for incoming connects and begins an asynchronous operation to accept connections.
        /// </summary>
        private void StartListening() {
            try {
                lock (_serverSocketLock) {
                    if (_serverSocket == null) return;

                    _serverSocket.Listen(1000);
                    _serverSocket.BeginAccept(AcceptCallback, null);
                }
                _listeningException = 0;
            }
            catch (Exception) {
                if (++_listeningException == LISTEN_EXCEPTION_MAX) {
                    GeneralExceptionLog($"Server has failed to start listening ({LISTEN_EXCEPTION_MAX}) and will NOT continue to try.");
                }
                else {
                    Thread.Sleep(1000);
                    StartListening();
                }

            }
        }

        /// <summary>
        /// Completes the asynchronous connection operation started in StartListening.
        /// </summary>
        /// <param name="ar">Object containing information on the status of the asynchronous operation.
        /// </param>
        private void AcceptCallback(IAsyncResult ar) {
            lock (_serverSocketLock) {
                if (_socketDisposed) return;
            }

            try {
                Socket handlerSocket = null;
                lock (_serverSocketLock) {
                    handlerSocket = _serverSocket.EndAccept(ar);
                }

                IPAddress clientIP = FindEndPoint(handlerSocket);
                if (clientIP == null) {
                    handlerSocket.Close();
                    return;
                }


                string userPass = "";
                lock (_usersLock) {
                    if (_users.Count > 0) {
                        if (_users.ContainsKey(clientIP)) {
                            userPass = _users[clientIP].Password;
                        }
                        else {
                            GeneralExceptionLog("PrimeServer.AcceptCallback: client with an invalid IP Address is trying to connect: " + clientIP);
                        }
                    }
                }

                ServerSpawn serverSpawn = null;
                lock (_clientListLock) {
                    _clientList.Add(serverSpawn = new ServerSpawn(clientIP,
                                                                userPass,
                                                                handlerSocket,
                                                                _handler,
                                                                _protocolType));
                }
                serverSpawn.Start(new BaseSpawn.OnDisconnect(OnSpawnDisconnect));
            }
            catch (Exception ex) {
                GeneralExceptionLog("PrimeServer.AcceptCallback: " + ex.Message);
            }
            finally {
                // Call StartListening to allow the server to listen for more incoming connection attempts.
                StartListening();
            }
        }

        private void OnSpawnDisconnect(BaseSpawn sender) {
            if (sender == null || !(sender is ServerSpawn)) return;
            sender.Stop();
            lock (_clientListLock) {
                _clientList.Remove(sender as ServerSpawn);
            }
        }

        /// <summary>
        /// Enqueues the given TransmissionObject to be sent to each client in the client list.
        /// </summary>
        /// <param name="transObj">The TransmissionObject that is to be sent to each client.
        /// </param>
        public void Broadcast(Packet obj) {
            try {
                // Enqueue the TransmissionObject to each client in the client list.
                List<ServerSpawn> lst = null;
                lock (_clientListLock) {
                    lst = _clientList;
                }
                foreach (ServerSpawn ss in lst) {
                    ss.EnqueueObject(obj);
                }
            }
            catch (Exception ex) {
                GeneralExceptionLog("PrimeServer.Broadcast Exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Enqueues a transmission object to be sent, in the server's pending object queue.
        /// </summary>
        /// <param param name="ip">IP address to which the transmission object must be sent.
        /// </param>
        /// <param name="transObj">TransmissionObject to be sent.
        /// </param>
        public void EnqueueObject(IPAddress ip, Packet obj) {
            if (ip == null) {
                GeneralExceptionLog("Could not find client to transmit object to: IPAddress is NULL");
                return;
            }
            List<ServerSpawn> spawns = null;
            lock (_clientListLock) {
                spawns = (from c in _clientList
                          where c.ClientIP.Equals(ip)
                          select c).ToList();
            }

            if (spawns == null || spawns.Count == 0) {
                GeneralExceptionLog("Could not find client to transmit object to: " + ip);
                return;
            }
            spawns.ForEach(x => x.EnqueueObject(obj));
        }

        /// <summary>
        /// Extracts the client's IP address from the socket connected to the client.
        /// </summary>
        /// <param name="soc">The socket connected to the client.
        /// </param>
        /// <returns>The client's IP.
        /// </returns>
        public IPAddress FindEndPoint(Socket soc) {
            if (soc == null || (!(soc.RemoteEndPoint is IPEndPoint))) {
                GeneralExceptionLog("BaseSpawn.FindClientIP: Invalid Socket/RemoteEndPoint");
                return null;
            }
            return (soc.RemoteEndPoint as IPEndPoint).Address;
        }

        private static List<IPAddress> GetValidIPAddress() {
            List<IPAddress> result = new List<IPAddress>();
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            NetworkInterface[] netInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface adapter in netInterfaces) {
                if (adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet) continue;

                var ipProps = adapter.GetIPProperties();
                foreach (IPAddressInformation ip in ipProps.UnicastAddresses) {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(ip.Address)) {
                        continue;
                    }

                    if (!System.Net.Dns.GetHostEntry(String.Empty).AddressList.Contains(ip.Address)) continue;

                    result.Add(ip.Address);
                }
            }
            return result;
        }

        /// <summary>
        /// Closes the connection to every client connected to this server.
        /// </summary>
        public void Stop() {
            lock (_usersLock) {
                _users.Clear();
            }

            lock (_clientListLock) {
                while (_clientList.Count > 0) {
                    try {
                        _clientList[0].Stop();
                    }
                    finally {
                        _clientList.RemoveAt(0);
                    }
                }
            }

            try {
                lock (_serverSocketLock) {
                    _socketDisposed = true;
                    _serverSocket.Close();
                    _serverSocket.Dispose();
                    _serverSocket = null;
                }
            }
            catch {
                // ignore
            }
            _hasStarted = false;
        }

        private void GeneralExceptionLog(string message) {
            _handler?.OnExceptionLog(this, message);
        }
    }
}
