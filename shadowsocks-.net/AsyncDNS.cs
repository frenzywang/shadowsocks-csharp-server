using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace shadowsocks_.net
{
    //Non thread safe wwww
    class DNSCbContext
    {
        public string host;
        public int port;

        public DNSCbContext(string _host, int _port)
        {
            host = _host;
            port = _port;
        }
    }
    class DNSCache
    {
        static DNSCache instence = null;
        Dictionary<string, DNSResult> result = new Dictionary<string, DNSResult>();
        private Mutex mut = new Mutex();
        public static DNSCache GetInstence()
        {
            if (instence == null)
            {
                instence = new DNSCache();
            }
            return instence;
        }

        public IPAddress Get(string host)
        {
            mut.WaitOne();
            CleanUp();
            if (result.ContainsKey(host))
            {
                IPAddress ipaddress = result[host].ipaddress;
                mut.ReleaseMutex();
                return ipaddress;
            }
            mut.ReleaseMutex();
            return null;
        }

        public void Put(string host, IPAddress ipAddress)
        {
            mut.WaitOne();
            {
                DNSResult dnsresult = new DNSResult(ipAddress);
                result[host] = dnsresult;
            }
            mut.ReleaseMutex();
        }

        public void CleanUp()
        {
            List<string> lst = new List<string>();
            DateTime now = DateTime.Now;
            foreach (KeyValuePair<string, DNSResult> a in result)
            {
                if ((now - a.Value.last_used).TotalSeconds >= 60)
                {
                    lst.Add(a.Key);
                }
            }
            foreach (string a in lst)
            {
                result.Remove(a);
            }
        }
        
    }

    class DNSResult
    {
        public DateTime last_used = DateTime.Now;
        public IPAddress ipaddress;

        public DNSResult(IPAddress _ipaddress){ ipaddress = _ipaddress; }
    }
}
