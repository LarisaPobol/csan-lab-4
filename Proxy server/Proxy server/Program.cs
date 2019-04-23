using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Proxy_server
{
    class Program
    {
        private const int port = 49000;

        static void Main(string[] args)
        {
            Proxy proxy = new Proxy(port);
            try
            {
                proxy.StartProxy();
            }
            catch
            {
                proxy.StopProxy();
            }
        }
    }
}
