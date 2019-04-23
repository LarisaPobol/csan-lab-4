using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace Proxy_server
{
    class Proxy
    {
        private int Port;
        private Thread listenThread = null;
        public bool stopProxy = false;

        public Proxy(int proxyPort)
        {
            this.Port = proxyPort;
        }

        public void StartProxy()
        {
            int backlog = (int)SocketOptionName.MaxConnections;
            IPEndPoint listenIPEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(listenIPEndPoint);
            listenSocket.Listen(backlog);
            while (true)
            {
                if (listenSocket.Poll(0, SelectMode.SelectRead))
                {
                    Socket requestSocket = listenSocket.Accept();
                    StartClientReceive(requestSocket);
                }
                if (stopProxy) break;
            }
        }

        private void StartClientReceive(Socket socket)
        {
            listenThread = new Thread(() => { ProcessSocket(socket); })
            {
                IsBackground = true
            };
            listenThread.Start();
        }

        private void ProcessSocket(Socket requestSocket)
        {
            using (requestSocket)
            {
                if (requestSocket.Connected)
                {
                    byte[] httpClientRequest = GetTcpMessage(requestSocket);
                    Regex myReg = new Regex(@"Host: (((?<host>.+?):(?<port>\d+?))|(?<host>.+?))\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    Match m = myReg.Match(System.Text.Encoding.ASCII.GetString(httpClientRequest));
                    string host = m.Groups["host"].Value;
                    int port = 0;
                    // если порта нет, то используем 80 по умолчанию
                    if (!int.TryParse(m.Groups["port"].Value, out port)) { port = 80; }

                    // получаем апишник по хосту
                    IPHostEntry myIPHostEntry = Dns.GetHostEntry(host);

                    // создаем точку доступа
                    IPEndPoint myIPEndPoint = new IPEndPoint(myIPHostEntry.AddressList[0], port);

                    // создаем сокет и передаем ему запрос
                    using (Socket myRerouting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        myRerouting.Connect(myIPEndPoint);
                        if (myRerouting.Send(httpClientRequest, httpClientRequest.Length, SocketFlags.None) != httpClientRequest.Length)
                        {
                            Console.WriteLine("При отправке данных удаленному серверу произошла ошибка...");
                        }
                        else
                        {
                            // получаем ответ
                            byte[] httpResponse = GetTcpMessage(myRerouting);
                            // передаем ответ обратно клиенту
                            if (httpResponse != null && httpResponse.Length > 0)
                            {
                                requestSocket.Send(httpResponse, httpResponse.Length, SocketFlags.None);
                            }
                        }
                    }
                    requestSocket.Close();
                }
            }
            }

        private byte[] GetTcpMessage(Socket socket)
        {
            // byte[] curr = new byte[1024];
            // byte[] res = { };
            //// EndPoint serv = new EndPoint(IPAddress.Any);
            // int recv;
            // do
            // {
            //     recv = socket.Receive(curr);
            //     res.Concat(curr);
            // }

            // revc = socket.Receive(res, tota)
            // while (recv != 0); 
            // return res;

            byte[] b = new byte[socket.ReceiveBufferSize];
            int len = 0;
            using (MemoryStream m = new MemoryStream())
            {
                while (socket.Poll(1000000, SelectMode.SelectRead) && (len = socket.Receive(b, socket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    m.Write(b, 0, len);
                }
                return m.ToArray();
            }

        }

        public void StopProxy()
        {
            stopProxy = true;
            //закрыть сокеты
            //закрыть потоки
        }
    }
}
