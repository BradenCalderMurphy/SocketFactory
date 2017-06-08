
namespace SocketFactory
{
    public interface IBaseSpawnHandler
    {
        void OnStart(BaseSpawn sender);
        void WhileConnected(BaseSpawn sender);
        void OnDisconnect(BaseSpawn sender, string message);
        void OnConnect(BaseSpawn sender);

        void OnRead(BaseSpawn sender, Packet packet);

        void OnReadStream(BaseSpawn sender, byte[] readBytes, StreamPacket packet);
        void OnCompleteStreamRead(BaseSpawn sender, StreamPacket packet);

        void OnExceptionLog(object sender, string message);
    }
}
