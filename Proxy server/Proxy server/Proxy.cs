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
        // private Thread listenThread = null;
        public bool stopProxy = false;
        private const int backlog = (int)SocketOptionName.MaxConnections;
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
                Console.WriteLine("Сервер запущен");
                while (true)
                {
                    if (listenSocket.Poll(0, SelectMode.SelectRead))
                    {
                        //Socket requestSocket = listenSocket.Accept();
                        //StartClientReceive(requestSocket);
                        // StartClientReceive(listenSocket.Accept());

                        Thread listenThread = new Thread(() => { ProcessSocket(listenSocket.Accept()); })
                        {
                            IsBackground = true
                        };
                        listenThread.Start();

                    }
                    if (stopProxy) break;
                }
            }
        }

        private void StartClientReceive(Socket socket)
        {
            //listenThread = new Thread(() => { ProcessSocket(socket); })
            //{
            //    IsBackground = true
            //};
            //listenThread.Start();
        }

        private void ProcessSocket(Socket requestSocket)
        {
            using (requestSocket)
            {
                if (requestSocket.Connected)
                {
                    //byte[] httpClientRequest = GetTcpMessage(requestSocket);
                    byte[] httpClientRequest;
                    string temp;
                    string[] requestFields;
                    string[] hostFields;
                    string hostField;
                    string host;
                    try
                    {
                        GetTcpMessage(requestSocket, out httpClientRequest);
                        temp = Encoding.ASCII.GetString(httpClientRequest);
                        requestFields = temp.Trim().Split(new char[] { '\r', '\n' });
                        hostField = requestFields.FirstOrDefault(x => x.Contains("Host"));
                        hostFields = hostField.Split(' ');
                        host = hostFields[1];

                        Console.WriteLine("Запрос: URL: " + hostFields[1] +" "+ httpClientRequest.Length);
                        IPHostEntry myIPHostEntry = Dns.GetHostEntry(hostFields[1]);
                        IPEndPoint myIPEndPoint = new IPEndPoint(myIPHostEntry.AddressList[0], 80);


                        using (Socket myRerouting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                        {
                            myRerouting.Connect(myIPEndPoint);
                            if (myRerouting.Send(httpClientRequest, httpClientRequest.Length, SocketFlags.None) != httpClientRequest.Length)
                            {
                                Console.WriteLine("Error");
                            }
                            else
                            {
                                byte[] httpResponse;
                                GetTcpMessage(myRerouting, out httpResponse);
                                requestSocket.Send(httpResponse, httpResponse.Length, SocketFlags.None);
                            }
                        }

                }
                    catch (SocketException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                requestSocket.Close();
                }
            }
        }

        private static void GetTcpMessage(Socket socket, out byte[] requestBytes)
        {
            byte[] buf = new byte[socket.ReceiveBufferSize];
            int recv = 0;
            using (MemoryStream requestMemoryStream = new MemoryStream())
            {
                while (socket.Poll(99999, SelectMode.SelectRead) && (recv = socket.Receive(buf, socket.ReceiveBufferSize, SocketFlags.None)) > 0)
                {
                    requestMemoryStream.Write(buf, 0, recv);
                }
                requestBytes = requestMemoryStream.ToArray();
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
