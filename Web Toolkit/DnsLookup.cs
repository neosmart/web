using DnsClient;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    static public class DnsLookup
    {
        private static IList<IPAddress> DnsServers = new[] {
            //"192.168.45.1",
            "8.8.8.8",
            /*"8.8.4.4",
            "1.1.1.1",
            "1.0.1.0",*/
        }.Select(IPAddress.Parse).ToList();

#if DEBUG
        private static LookupClient _dnsClient = new LookupClient(DnsServers.ToArray());
#else
        private static LookupClient _dnsClient = new LookupClient();
#endif

        public static IEnumerable<string> GetMXRecords(string domain)
        {
            var records = _dnsClient.Query(domain, QueryType.MX)
                .Answers.MxRecords();

            return records.Select(r => r.Exchange.Value);
        }

        public static async ValueTask<IEnumerable<string>> GetMXRecordsAsync(string domain)
        {
            var records = (await _dnsClient.QueryAsync(domain, QueryType.MX))
                .Answers.MxRecords();

            return records.Select(r => r.Exchange.Value);
        }

        public static IEnumerable<IPAddress> GetIpAddresses(string domain)
        {
            var result = _dnsClient.Query(domain, QueryType.A);

            //Debug.Assert(task.IsCompleted);
            var records = result.Answers.ARecords();
            return records.Select(r => r.Address);
        }

        public static async ValueTask<IEnumerable<IPAddress>> GetIpAddressesAsync(string domain)
        {
            var result = await _dnsClient.QueryAsync(domain, QueryType.A);

            //Debug.Assert(task.IsCompleted);
            var records = result.Answers.ARecords();
            return records.Select(r => r.Address);
        }
    }
}