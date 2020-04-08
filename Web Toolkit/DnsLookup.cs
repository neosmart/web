using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    static public class DnsLookup
    {
        private static IList<IPAddress> DnsServers = new[] {
            "192.168.45.1",
            "8.8.8.8",
            "1.1.1.1",
            "75.75.75.75",
        }.Select(ip => IPAddress.Parse(ip)).ToList();

        private static DnsClient _dnsClient = new DnsClient(DnsServers, 5000);
        private static string[] BlankResult = new string[] { };

        public static IEnumerable<string> GetMXRecords(string domain, out bool found)
        {
            var result = _dnsClient.Resolve(DomainName.Parse(domain), RecordType.Mx);
            if (result?.AnswerRecords == null)
            {
                found = false;
                return BlankResult;
            }

            var records = result.AnswerRecords.OfType<MxRecord>();
            found = records.Any();
            return records.Select(r => r.ExchangeDomainName.ToString());
        }

        public static bool GetIpAddresses(string domain, out IPAddress[] addresses)
        {
            if (!DomainName.TryParse(domain, out var parsedDomain))
            {
                addresses = null;
                return false;
            }

            var result = _dnsClient.Resolve(parsedDomain);
            if (result?.AnswerRecords == null)
            {
                addresses = null;
                return false;
            }

            //Debug.Assert(task.IsCompleted);
            var records = result.AnswerRecords.OfType<ARecord>();
            if (!records.Any())
            {
                addresses = null;
                return false;
            }

            addresses = records.Select(r => r.Address).ToArray();
            return true;
        }
    }
}