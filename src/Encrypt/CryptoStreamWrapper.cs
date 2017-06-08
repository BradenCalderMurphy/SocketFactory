using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SocketFactory.Encrypt
{
    class CryptoStreamWrapper : CryptoStream
    {
        private readonly ICryptoTransform _transform;

        public CryptoStreamWrapper(Stream stream, ICryptoTransform transform, CryptoStreamMode mode)
            : base(stream, transform, mode)
        {
            _transform = transform;
        }

        protected override void Dispose(bool disposing)
        {
            _transform?.Dispose();
            base.Dispose(false);
        }
    }
}