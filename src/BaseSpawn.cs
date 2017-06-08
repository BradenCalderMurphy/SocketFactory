using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using SocketFactory.Encrypt;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using SocketFactory.Environment;

namespace SocketFactory {
    public abstract class BaseSpawn {
        #region Globals

        public const int DEFAULT_PORT = 14001;

        private const uint PING_TIMEOUT = 15; // seconds
        private const int PING_SEND = 3; // seconds
        private const int IS_STOPPING_TIMEOUT = 3; // seconds

        private ConcurrentQueue<Packet> _writeQueue = new ConcurrentQueue<Packet>();

        private object _writeInProgressLock = new object();
        private bool _writeInProgress = false;
        private bool CanStartWriting() {
            lock (_writeInProgressLock) {
                if (_writeInProgress) {
                    return false;
                }
                _writeInProgress = true;
                return true;
            }
        }
        private void StopWriting() {
            lock (_writeInProgressLock) {
                _writeInProgress = false;
            }
        }

        private object _readInProgressLock = new object();
        private bool _readInProgress = false;
        private bool CanStartReading() {
            lock (_readInProgressLock) {
                if (_readInProgress) {
                    return false;
                }
                _readInProgress = true;
                return true;
            }
        }
        private void StopReading() {
            lock (_readInProgressLock) {
                _readInProgress = false;
            }
        }

        private object _isRunninglock = new object();
        private bool _isRunning = false;
        public bool IsRunning {
            private set {
                lock (_isRunninglock) {
                    _isRunning = value;
                }
            }
            get {
                lock (_isRunninglock) {
                    return _isRunning;
                }
            }
        }

        private object _isStoppingLock = new object();
        private bool _isStopping;
        private bool IsStopping {
            set {
                lock (_isStoppingLock) {
                    _isStopping = value;
                }
            }
            get {
                lock (_isStoppingLock) {
                    return _isStopping;
                }
            }
        }

        private object _connectedLock = new object();
        private bool _connected = false;

        internal delegate void OnDisconnect(BaseSpawn sender);
        private OnDisconnect _onDisconnect;

        public NetworkStream Stream {
            get {
                return _stream;
            }
        }

        public Socket Socket {
            get {
                return _socket;
            }
        }

        /// some rules: 
        /// basespawn class sets connected to false in beginning
        /// inherited classes are now allowed access to the stream/socket variables
        protected bool Connected {
            private set {
                bool sendDC = false;
                bool sendC = false;
                string sendDCMessage = "";
                lock (_connectedLock) {
                    if (!value && _connected) {
                        sendDC = true;
                        try {
                            _socket.Shutdown(SocketShutdown.Both);
                            _socket.Disconnect(true);
                            sendDCMessage = "Successfully Disconnected.";
                        }
                        catch (Exception ex) {
                            sendDCMessage = ex.Message;
                            GeneralExceptionLog("ClientSpawn.Failed to shutdown/disconnect: " + ex.Message);
                        }
                    }
                    else if (value && !_connected) {
                        sendC = true;
                    }
                    _connected = value;
                }

                if (sendDC) {
                    _onDisconnect?.Invoke(this);
                    _handler?.OnDisconnect(this, sendDCMessage);
                    _protocol?.OnDisconnect(this);
                }
                else if (sendC) {
                    _handler?.OnConnect(this);
                    _protocol?.OnConnect(this);
                }
            }
            get {
                lock (_connectedLock) {
                    return _connected;
                }
            }
        }

        private Encryption _encryption;
        private object _lastPingLock = new object();
        private DateTimeEnvironment _lastPing;
        private DateTimeEnvironment _sendPing;
        private CommsThread _commsThread;
        private NetworkStream _stream;
        private readonly Socket _socket;
        private readonly IBaseSpawnHandler _handler;
        private readonly IServerProtocol _protocol;
        #endregion

        internal BaseSpawn(Socket socket, IBaseSpawnHandler handler, Type protocolType) {
            _socket = socket ?? throw new ArgumentNullException("Socket cannot be null.");
            _handler = handler;
            if (protocolType != null) {
                _protocol = (IServerProtocol)Activator.CreateInstance(protocolType);
            }
        }

        #region Socket/Connection Methods
        protected Socket GetSocket() {
            if (Connected == false) return _socket;
            throw new Exception("First disconnect before accessing socket");
        }

        protected void SetConnected(string password) {
            if (String.IsNullOrWhiteSpace(password)) {
                _encryption = null;
            }
            else {
                _encryption = new Encryption(password);
            }

            while (_writeQueue.Count > 0) {
                Packet result;
                _writeQueue.TryDequeue(out result);
            }

            _stream = new NetworkStream(_socket, true);
            lock (_lastPingLock) {
                _lastPing = DateTimeEnvironment.Now;
            }
            Connected = true;
            ReceiveObject();
            Task.Factory.StartNew(() => {
                SendObject();
            });
        }

        public bool Start() {
            return Start(null);
        }

        internal bool Start(OnDisconnect onDisconnect) {
            if (this.IsStopping) return false;
            if (IsRunning) return true;
            _onDisconnect = onDisconnect;
            this.IsRunning = true;
            _commsThread = new CommsThread(Process, new CommsThread.OnExceptionLog(GeneralExceptionLog), 1000);
            BaseStart();
            _handler?.OnStart(this);
            return true;
        }

        protected abstract void BaseStart();

        private void UpdatePing() {
            lock (_lastPingLock) {
                _lastPing = DateTimeEnvironment.Now;
            }
        }

        protected abstract void StopBase();

        public void Stop() {
            Stop(true, true);
        }

        private async void Stop(bool sendRequestToShutdown, bool closeWithDelay) {

            if (this.IsStopping) return;

            if (sendRequestToShutdown) {
                this.EnqueueObject(new InternalPacket() { PacketType = InternalPacket.InternalPacketType.RequestToShutdown });
            }
            this.IsStopping = true;
            await Task.Factory.StartNew(() => {
                if (closeWithDelay) {
                    DateTimeEnvironment dt = DateTimeEnvironment.Now;
                    while (DateTimeEnvironment.Now.Subtract(dt).TotalSeconds <= IS_STOPPING_TIMEOUT) {
                        Thread.Sleep(20);
                    }
                }
                InternalStop();
            });
        }

        private void InternalStop() {
            try {
                if (this._commsThread != null) {
                    this._commsThread.Stop();
                    this._commsThread = null;
                }
                this.Connected = false;
                StopBase();
            }
            catch (Exception ex) {
                GeneralExceptionLog($"Error Disposing {this.GetType().ToString()} {ex.Message}");
            }
            finally {
                this.IsStopping = false;
                this.IsRunning = false;
            }
        }

        protected abstract void Running();

        private void Process() {
            Running();
            if (Connected) _handler?.WhileConnected(this);

            DateTimeEnvironment lastPing;
            lock (_lastPingLock) {
                lastPing = _lastPing;
            }

            TimeSpanEnvironment ts = DateTimeEnvironment.Now.Subtract(lastPing);
            if (Connected) {
                if (ts.TotalSeconds < PING_TIMEOUT) {
                    ts = DateTimeEnvironment.Now.Subtract(_sendPing);
                    if (ts.TotalSeconds >= PING_SEND) {
                        EnqueueObject(new InternalPacket() { PacketType = InternalPacket.InternalPacketType.Ping });
                        _sendPing = DateTimeEnvironment.Now;
                    }
                }
                else {
                    GeneralExceptionLog("SocketBase.Process: Ping Timeout.");
                    this.Connected = false;
                }
            }
        }
        #endregion

        #region Send/Receive
        /// <summary>
        /// Enqueue an object on to the pending write queue, to be written to the network stream.
        /// </summary>
        /// <typeparam name="T">Type of object that will be added to the queue.</typeparam>
        /// <param name="obj">Object to be added to the queue.</param>
        public void EnqueueObject(Packet packet) {
            if (!this.IsRunning) return;
            if (this.IsStopping) return;
            if (!this.Connected) return;

            if (packet == null) return;
            if(packet is InternalPacket &&
              (packet as InternalPacket).PacketType == InternalPacket.InternalPacketType.RequestToShutdown) {
                while(_writeQueue.Count() > 0) {
                    _writeQueue.TryDequeue(out Packet p);
                }
            }
            _writeQueue.Enqueue(packet);
            Task.Factory.StartNew(() => {
                SendObject();
            });
        }

        /// <summary>
        /// If a valid connection is active, and there are remaining WriteObjects to be written,
        /// attempt to send the next Transmission Object in the write queue. The Transmission
        /// Object is encrypted into a DataTransport Object, before being serialized onto
        /// the stream. If there are still objects in the write queue, SendObject() is called
        /// again to attempt to send the next object.
        /// </summary>
        private void SendObject() {
            if (!this.IsRunning) return;
            if (!Connected) return;
            if (!CanStartWriting()) return;

            Packet pckt = null;

            try {
                if (_writeQueue.IsEmpty) { return; }
                if (!_writeQueue.TryPeek(out pckt)) return;

                IFormatter formatter = new BinaryFormatter();
                string message = "";
                bool removePacket = true;

                if (_protocol != null) {
                    _protocol.OnSendPacket(this, pckt, out removePacket);
                }
                else if (_encryption == null) {
                    formatter.Serialize(_stream, pckt);
                }
                else {
                    DataTransport data = _encryption.Encrypt(pckt, out message);
                    if (!String.IsNullOrWhiteSpace(message)) {
                        GeneralExceptionLog("Could not encrypt data: " + message);
                        Connected = false;
                        return;
                    }
                    formatter.Serialize(_stream, data);
                }

                if (removePacket) {
                    Packet temp = null;
                    if (!_writeQueue.TryDequeue(out temp)) {
                        GeneralExceptionLog("SocketBase.SendObject: Failed to dequeue packet.");
                    }
                }
                else {
                    Thread.Sleep(20);
                }

                //if a stream needs to be sent, call the WriteStream method
                if (_protocol == null && pckt is StreamPacket) {
                    WriteStream(pckt as StreamPacket);
                }
            }
            catch (Exception ex) {
                GeneralExceptionLog("SocketBase.SendObject: " + ex.Message);
                this.Connected = false;
                Packet temp = null;
                if (!_writeQueue.TryDequeue(out temp)) {
                    GeneralExceptionLog("SocketBase.SendObject: Failed to dequeue packet.");
                }
            }
            finally {
                if (pckt is StreamPacket) {
                    (pckt as StreamPacket).StreamToSend.Dispose();
                }
                StopWriting();
                if (_writeQueue.Count > 0 && this.Connected) {
                    Task.Factory.StartNew(() => {
                        SendObject();
                    });
                }
            }
        }

        /// <summary>
        /// Writes a stream to the network stream.
        /// </summary>
        /// <param name="inputStream">The stream to write to the network stream.</param>
        private void WriteStream(StreamPacket sPacket) {
            if (sPacket.StreamToSend == null) return;

            if (_encryption == null) {
                try {
                    sPacket.StreamToSend.CopyTo(_stream, Encryption.BUFFER_SEGMENT_SIZE);
                }
                catch (Exception ex) {
                    _handler?.OnExceptionLog(this, "Error:\t" + ex.Message);
                }
            }
            else {
                //The cryptostream is used to encrypt the stream before sending it
                CryptoStreamWrapper crypStream = null;
                try {
                    //create the cryptostream with a static method from the Encryption class
                    crypStream = _encryption.CreateEncrypt(_stream);
                    sPacket.StreamToSend.CopyTo(crypStream, Encryption.BUFFER_SEGMENT_SIZE);

                    //send a stream segment to ensure that the final transmission gets read
                    //on the other side
                    using (MemoryStream fillStream = new MemoryStream(Encryption.BUFFER_SEGMENT_SIZE)) {
                        for (int i = 0; i < fillStream.Capacity; i++)
                            fillStream.WriteByte(0);
                        fillStream.Position = 0;
                        fillStream.CopyTo(crypStream);
                    }

                    //if the last transmission was a stream, send a series of bytes to indicate
                    //that a packet is now being sent
                    foreach (byte b in EndOfStreamHandler.EndOfStreamBytes) {
                        _stream.WriteByte(b);
                    }
                }
                catch (Exception ex) {
                    _handler?.OnExceptionLog(this, "Error:\t" + ex.Message);
                }
                finally {
                    crypStream?.Close();
                    crypStream?.Dispose();
                    crypStream = null;
                }
            }
        }

        /// <summary>
        /// Asynchronously reads an object off the Network Stream, and handles it if
        /// the read is successful. ReceiveObject is called again at the end of the method
        /// to read the next object from the Network Stream.
        /// </summary>
        private async void ReceiveObject() {
            if (!IsRunning) return;
            if (!Connected) return;
            if (!CanStartReading()) return;
            if (IsStopping) return;

            try {
                // Wait for the Transmission Object to be read off the Network Stream asynchronously.
                // If no error occured in the reading, call HandleReceivedObject to process it.
                Packet pckt = null;
                await Task.Factory.StartNew(() => {
                    try {

                        IFormatter formatter = new BinaryFormatter();
                        if (_protocol != null) {
                            pckt = _protocol.OnReceiveData(this);
                        }
                        // If encryption is not enabled receive data without decrypting.
                        else if (_encryption == null) {
                            pckt = (Packet)formatter.Deserialize(_stream);
                        }

                        // If encryption is enabled, decrypt data before receiving.
                        else {
                            DataTransport data = (DataTransport)formatter.Deserialize(_stream);
                            string message = "";
                            pckt = _encryption.Decrypt<Packet>(data, out message);
                            if (!string.IsNullOrWhiteSpace(message)) {
                                GeneralExceptionLog("SocketFactory - Could not decrypt received object. " + message);
                                Connected = false;
                            }
                        }

                        //if packet is a stream packet read the incoming stream
                        if (pckt is StreamPacket) {
                            ReadStream(pckt as StreamPacket);
                        }
                    }
                    catch (Exception ex) {
                        if (!this.IsStopping) {
                            pckt = null;
                            Connected = false;
                            GeneralExceptionLog("SocketBase.ReceiveObject: " + this.GetType() + ", " + ex.Message + "," + ex.InnerException + "," + ex.StackTrace);
                        }
                     }
                });

                if (pckt == null) {
                    Thread.Sleep(20);
                    return;
                }

                UpdatePing();

                if (pckt.GetType().Equals(typeof(InternalPacket))) {
                    InternalPacket intPacket = (pckt as InternalPacket);
                    switch (intPacket.PacketType) {
                        case InternalPacket.InternalPacketType.Ping:
                            // do nothing
                            break;
                        case InternalPacket.InternalPacketType.Error:
                            GeneralExceptionLog(intPacket.ErrorMessage);
                            break;
                        case InternalPacket.InternalPacketType.RequestToShutdown:
                            this.Stop(false, false);
                            return; // note the return
                        default:
                            GeneralExceptionLog("Invalid Packet Type: " + intPacket.PacketType.ToString());
                            break;
                    }
                }
                else {
                    _handler?.OnRead(this, pckt);
                }

            }
            finally {
                StopReading();
                ReceiveObject();
            }
        }

        private void ReadStream(StreamPacket sPacket) {
            long totalRead = 0;
            int toRead = 0;

            CryptoStreamWrapper crypStream = null;
            try {
                try {
                    //read stream
                    using (MemoryStream memStream = new MemoryStream(Encryption.BUFFER_SEGMENT_SIZE)) {
                        if (_encryption != null) {
                            crypStream = _encryption.CreateDecrypt(_stream);
                        }

                        do {
                            toRead = (int)((totalRead + Encryption.BUFFER_SEGMENT_SIZE > sPacket.StreamLength) ?
                                (sPacket.StreamLength - totalRead) : Encryption.BUFFER_SEGMENT_SIZE);

                            Array.Clear(memStream.GetBuffer(), 0, memStream.GetBuffer().Length);
                            memStream.SetLength(toRead);

                            if (_encryption == null) {
                                totalRead += _stream.Read(memStream.GetBuffer(), 0, toRead);
                            }
                            else {
                                totalRead += crypStream.Read(memStream.GetBuffer(), 0, toRead);
                            }
                            _handler?.OnReadStream(this, memStream.ToArray(), sPacket);
                        }
                        while (totalRead < sPacket.StreamLength);

                        //handle end of stream
                        if (_encryption != null) {
                            List<byte> eosBuffer = new List<byte>(EndOfStreamHandler.EndOfStreamBytesLength);
                            while (true) {
                                if (eosBuffer.Count == EndOfStreamHandler.EndOfStreamBytesLength) eosBuffer.RemoveAt(0);
                                eosBuffer.Add((byte)_stream.ReadByte());
                                if (eosBuffer.ToArray().SequenceEqual(EndOfStreamHandler.EndOfStreamBytes)) break;
                            }
                        }
                    }
                }
                finally {
                    crypStream?.Dispose();
                }
            }
            catch (Exception ex) {
                GeneralExceptionLog("Error:\t" + ex.Message);
                return;
            }
            finally {
                _handler?.OnCompleteStreamRead(this, sPacket);
            }
        }

        #endregion

        public void GeneralExceptionLog(string message) {
            if (String.IsNullOrWhiteSpace(message)) return;
            _handler?.OnExceptionLog(this, message);
        }
    }
}