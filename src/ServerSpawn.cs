using System;
using System.Net;
using System.Net.Sockets;

namespace SocketFactory {

    public class ServerSpawn : BaseSpawn
    {
        private readonly string _clientPassword;
        public IPAddress ClientIP { get; set; }

        internal ServerSpawn(IPAddress clientIP,
                        string clientPassword,
                        Socket clientSocket,
                        IBaseSpawnHandler handler)
            : this(clientIP, clientPassword, clientSocket, handler, null)
        {
        }

        internal ServerSpawn(IPAddress clientIP,
                string clientPassword,
                Socket clientSocket,
                IBaseSpawnHandler handler,
                Type protocolType)
            : base(clientSocket, handler, protocolType)
        {
            _clientPassword = clientPassword;
            ClientIP = clientIP;
        }

        protected override void BaseStart()
        {
            SetConnected(_clientPassword);
        }

        #region Inherited abstract methods
        protected override void StopBase()
        {

        }

        protected override void Running()
        {

        }

        #endregion
    }
}
