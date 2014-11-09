using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using shadowsocks_csharp;
using System.Threading;
using System.Timers;


namespace shadowsocks_.net
{
    class Server
    {
        private Config config;
        private Socket listener;

        public Server(Config config)
        {
            this.config = config;
        }

        public void Start()
        {
            listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint severEndPoint = new IPEndPoint(0, config.server_port);
            // Bind the socket to the sever endpoint and listen for incoming connections.
            listener.Bind(severEndPoint);
            //half open count
            listener.Listen(10);

            // Start an asynchronous socket to listen for connections.
            Console.WriteLine("Waiting for a connection...");
            listener.BeginAccept(
                new AsyncCallback(AcceptCallback),
                listener);
        }

        public void Stop()
        {
            listener.Close();
        }

        //local connected
        public void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket conn = listener.EndAccept(ar);
                Handler handler = new Handler();
                handler.connection = conn;
                handler.encryptor = new Encryptor(config.method, config.password);
                handler.config = config;
                handler.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);
            }
        }

    }


    class Handler
    {
        public Encryptor encryptor = null;
        public Config config;
        // Client  socket.
        public Socket remote;
        public Socket connection;
        // Size of receive buffer.
        public const int BufferSize = 1500;
        // remote receive buffer
        public byte[] remoteBuffer = new byte[BufferSize];
        // connection receive buffer
        public byte[] connetionBuffer = new byte[BufferSize];
        // connection Stage
        private int stage = 0;
        //remote addr
        private string destAddr = null;

        public Handler()
        {

        }

        public void Start()
        {
            try
            {
                int ivLent = encryptor.GetivLen();
                if (ivLent > 0)
                {
                    connection.BeginReceive(this.connetionBuffer, 0, ivLent, 0,
                        new AsyncCallback(handshakeReceiveCallback), null);
                }
                else
                {
                    stage = 1;
                    connection.BeginReceive(this.connetionBuffer, 0, 1, 0,
                        new AsyncCallback(handshakeReceiveCallback), null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        public void Close()
        {
            if (encryptor == null)
            {
                return;
            }
            else
            {
                encryptor.Dispose();
                encryptor = null;
            }

            Console.WriteLine("close:" + connection.RemoteEndPoint.ToString());
            
            if (connection != null)
            {
                try
                {
                    connection.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                }
            }
            if (remote != null)
            {
                try
                {
                    remote.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                }
            }
        }

        private void handshakeReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = connection.EndReceive(ar);
                //Console.WriteLine("bytesRead" + bytesRead.ToString() + " stage" + stage.ToString());
                if (stage == 0)
                {
                    //recv numbers of ivlen data
                    byte[] iv = encryptor.Decrypt(this.connetionBuffer, bytesRead);
                    //Decrypt sucessful
                    //iv
                    stage = 1;
                    connection.BeginReceive(this.connetionBuffer, 0, 1, 0,
                        new AsyncCallback(handshakeReceiveCallback), null);
                }
                else if (stage == 1)
                {
                    byte[] buff = encryptor.Decrypt(this.connetionBuffer, bytesRead);
                    //Decrypt sucessful
                    //addrtype
                    char addrtype = (char)buff[0];
                    if (addrtype == 1)
                    {
                        //type of ipv4
                        stage = 4;
                        connection.BeginReceive(this.connetionBuffer, 0, 4, 0,
                            new AsyncCallback(handshakeReceiveCallback), null);

                    }
                    else if (addrtype == 3)
                    {
                        //type of url
                        stage = 3;
                        connection.BeginReceive(this.connetionBuffer, 0, 1, 0,
                            new AsyncCallback(handshakeReceiveCallback), null);
                    }
                    else if (addrtype == 4)
                    {
                        //type of ipv6
                        stage = 4;
                        connection.BeginReceive(this.connetionBuffer, 0, 16, 0,
                            new AsyncCallback(handshakeReceiveCallback), null);
                    }
                    else
                    {
                        throw new Exception("Error Socket5 AddrType");
                    }
                }
                else if (stage == 3)
                {
                    //addr len
                    byte[] buff = encryptor.Decrypt(this.connetionBuffer, bytesRead);
                    stage = 4;
                    //recv addr
                    connection.BeginReceive(this.connetionBuffer, 0, buff[0], 0,
                        new AsyncCallback(handshakeReceiveCallback), null);
                }
                else if (stage == 4)
                {
                    //addr
                    byte[] buff = encryptor.Decrypt(this.connetionBuffer, bytesRead);
                    destAddr = ASCIIEncoding.Default.GetString(buff);
                    stage = 5;
                    connection.BeginReceive(this.connetionBuffer, 0, 2, 0,
                        new AsyncCallback(handshakeReceiveCallback), null);
                }
                else if (stage == 5)
                {
                    //port
                    byte[] buff = encryptor.Decrypt(this.connetionBuffer, bytesRead);
                    int port = (int)(buff[0] << 8) + (int)buff[1];

                    stage = 6;

                    //Begin to connect remote
                    IPAddress ipAddress;
                    bool parsed = IPAddress.TryParse(destAddr, out ipAddress);
                    if (!parsed)
                    {
                        IPAddress cache_ipAddress = DNSCache.GetInstence().Get(destAddr);
                        if (cache_ipAddress == null)
                        {
                            DNSCbContext ct = new DNSCbContext(destAddr, port);
                            Dns.BeginGetHostEntry(destAddr, new AsyncCallback(GetHostEntryCallback), ct);
                            return;
                        }
                        ipAddress = cache_ipAddress;
                    }
                    
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                    remote = new Socket(ipAddress.AddressFamily,
                        SocketType.Stream, ProtocolType.Tcp);

                    remote.BeginConnect(remoteEP,
                        new AsyncCallback(remoteConnectCallback), null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        public void GetHostEntryCallback(IAsyncResult ar)
        {
            DNSCbContext ct = (DNSCbContext)ar.AsyncState;
            IPHostEntry ipHostInfo = Dns.EndGetHostEntry(ar);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            DNSCache.GetInstence().Put(ct.host, ipAddress);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, ct.port);

            remote = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            remote.BeginConnect(remoteEP,
                new AsyncCallback(remoteConnectCallback), null);
        }

        private void remoteConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Complete the connection.
                remote.EndConnect(ar);

                Console.WriteLine("Connected to {0}",
                    remote.RemoteEndPoint.ToString());

                connection.BeginReceive(connetionBuffer, 0, BufferSize, 0,
                    new AsyncCallback(ConnectionReceiveCallback), null);
                remote.BeginReceive(remoteBuffer, 0, BufferSize, 0,
                    new AsyncCallback(RemoteReceiveCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        private void ConnectionReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = connection.EndReceive(ar);
                Console.WriteLine("bytesRead from client: " + bytesRead.ToString());
                if (encryptor == null)
                    return;
                if (bytesRead > 0)
                {
                    byte[] buf = encryptor.Decrypt(connetionBuffer, bytesRead);
                    remote.BeginSend(buf, 0, buf.Length, 0, new AsyncCallback(RemoteSendCallback), null);
                }
                else
                {
                    Console.WriteLine("client closed");
                    this.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        private void RemoteReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int bytesRead = remote.EndReceive(ar);
                Console.WriteLine("bytesRead from remote: " + bytesRead.ToString());
                if (encryptor == null)
                    return;
                if (bytesRead > 0)
                {
                    byte[] buf = encryptor.Encrypt(remoteBuffer, bytesRead);
                    connection.BeginSend(buf, 0, buf.Length, 0, new AsyncCallback(ConnectionSendCallback), null);
                }
                else
                {
                    //remote closed
                    Console.WriteLine("remote closed");
                    this.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        private void RemoteSendCallback(IAsyncResult ar)
        {
            try
            {
                int bytesSend = remote.EndSend(ar);
                Console.WriteLine("bytesSend to remote: " + bytesSend.ToString());

                connection.BeginReceive(this.connetionBuffer, 0, BufferSize, 0,
                    new AsyncCallback(ConnectionReceiveCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }

        private void ConnectionSendCallback(IAsyncResult ar)
        {
            try
            {
                int bytesSend = connection.EndSend(ar);
                Console.WriteLine("bytesSend to client: " + bytesSend.ToString());

                remote.BeginReceive(this.remoteBuffer, 0, BufferSize, 0,
                    new AsyncCallback(RemoteReceiveCallback), null);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                this.Close();
            }
        }
    }

}
