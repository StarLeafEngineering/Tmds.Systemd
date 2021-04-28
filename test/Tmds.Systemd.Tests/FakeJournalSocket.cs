using System;
using System.IO;
using System.Net.Sockets;
using Xunit;

namespace Tmds.Systemd.Tests
{
    public class FakeJournalSocket: IDisposable
    {
        public readonly string SocketPath;
        public readonly Socket Socket;

        public FakeJournalSocket()
        {
            SocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);

            Socket.Blocking = false;
            Socket.Bind(new UnixDomainSocketEndPoint(SocketPath));
            Journal.ConfigureJournalSocket(SocketPath);

            Assert.True(Journal.IsAvailable);
        }

        public void Dispose()
        {
            Socket.Dispose();
        }
    }
}
