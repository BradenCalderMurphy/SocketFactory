namespace SocketFactory {

    public interface IServerProtocol
    {
        void OnSendPacket(BaseSpawn sender, Packet packet, out bool removePacket);
        Packet OnReceiveData(BaseSpawn sender);
        void OnDisconnect(BaseSpawn sender);
        void OnConnect(BaseSpawn sender);
    }
}
