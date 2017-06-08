using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SocketFactory {
    public class AllowedUser {

        public AllowedUser(IPAddress ip, string password)
        {
            this.IP = ip;
            this.Password = password;
        }

        public AllowedUser(IPAddress ip)
            : this(ip, "")
        {

        }

        public bool HasPassword
        {
            get
            {
                return !String.IsNullOrWhiteSpace(this.Password);
            }
        }

        public IPAddress IP { private set; get; }
        public string Password { private set; get; }
    }
}
