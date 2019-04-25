using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Configuration;


namespace Proxy_server
{
    class Proxy : IDisposable
    {
        public void Dispose()
        {
            StopProxy();
        }

        private int Port;
        public bool stopProxy = false;
        private const int backlog = (int)SocketOptionName.MaxConnections;
        private const int sendPort = 80;
        public Proxy(int proxyPort)
        {
            this.Port = proxyPort;
        }

        public void StartProxy()
        {
            using (Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint listenIPEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
                listenSocket.Bind(listenIPEndPoint);
                listenSocket.Listen(backlog);
                while (true)
                {
                    if (listenSocket.Poll(0, SelectMode.SelectRead))
                    {
                        StartClientReceive(listenSocket.Accept());
                    }
                    if (stopProxy) break;
                }
            }
        }

        private void StartClientReceive(Socket socket)
        {
            Thread listenThread = new Thread(() => { ProcessSocket(socket); })
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
                    try
                    {
                        bool isForbidden = false;
                        byte[] httpByteArray;
                        string[] httpFields;
                        string[] hostFields;
                        string hostField;
                        string host;
                        string[] responseCode;
                        GetMessageFromSocket(requestSocket, out httpByteArray);
                        httpFields = SplitHttpToArray(httpByteArray);
                        hostField = httpFields.FirstOrDefault(x => x.Contains("Host"));
                        if (hostField == null) return;
                        hostFields = hostField.Split(' ');
                        host = string.Copy(hostFields[1]);
                        isForbidden = IsBlocked(host);
                        if (isForbidden)
                        {
                            SendErrorPage(requestSocket, host);
                            Console.WriteLine(" {0} заблокирован", host);
                            return;
                        }
                        else
                        {
                            Console.WriteLine("Запрос: " + hostField);
                            IPHostEntry ipHostEntry = Dns.GetHostEntry(host);
                            IPEndPoint ipEndPoint = new IPEndPoint(ipHostEntry.AddressList[0], sendPort);
                            using (Socket replySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                            {
                                replySocket.Connect(ipEndPoint);
                                if (replySocket.Send(httpByteArray, httpByteArray.Length, SocketFlags.None) != httpByteArray.Length)
                                {
                                    Console.WriteLine("Не удалось связаться с сервером");
                                }
                                else
                                {
                                    byte[] httpResponse;
                                    GetMessageFromSocket(replySocket, out httpResponse);
                                    requestSocket.Send(httpResponse, httpResponse.Length, SocketFlags.None);
                                    httpFields = SplitHttpToArray(httpResponse);
                                    responseCode = httpFields[0].Split(' ');
                                    if (responseCode == null) return;
                                    Console.WriteLine("Ответ сервера: код: " + responseCode[1]);
                                }
                            }
                        }
                        requestSocket.Close();
                    }
                    catch (Exception ex)
                    {
                       // Console.WriteLine(ex.Message);
                    }

                }
            }
        }

        private static void GetMessageFromSocket(Socket socket, out byte[] requestBytes)
        {
            byte[] buf = new byte[socket.ReceiveBufferSize];
            int recv = 0;
            using (MemoryStream requestMemoryStream = new MemoryStream())
            {
                while (socket.Poll(999999, SelectMode.SelectRead) && (recv = socket.Receive(buf, socket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    requestMemoryStream.Write(buf, 0, recv);
                }
                requestBytes = requestMemoryStream.ToArray();
            }
        }

        private string[] SplitHttpToArray(byte[] HttpHeading)
        {
            string strHttp;
            string[] resArray;
            strHttp = Encoding.ASCII.GetString(HttpHeading);
            resArray = strHttp.Trim().Split(new char[] { '\r', '\n' });
            return resArray;
        }
        
        private bool IsBlocked(string host)
        {
            var blacklist = ConfigurationManager.AppSettings;
            foreach (var key in blacklist.AllKeys)
            {
                if (host.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }

        private void SendErrorPage(Socket socket, string host)
        {
            string htmlBody = "<html><body><h1>Forbidden</h1><br><h2 style = \" color: red\">" + host + " is blocked</h2></body></html>";
            //File.ReadAllText()
            byte[] errorBodyBytes = Encoding.ASCII.GetBytes(htmlBody);
            socket.Send(errorBodyBytes, errorBodyBytes.Length, SocketFlags.None);
        }
        public void StopProxy()
        {
            stopProxy = true;
        }
    }
}
