namespace SocketFactory {

    public class PrimeServer<T> : PrimeServer where T : IServerProtocol {
        public PrimeServer(IBaseSpawnHandler handler)
             : base(handler, typeof(T)) {

        }

        public PrimeServer()
          : this(null) {

        }
    }
}
