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
        private static string[] BlankResult = new string[] { };
        public static IEnumerable<string> GetMXRecords(string domain, out bool found, int timeout = 60*1000)
        {
            var tokenSource = new CancellationTokenSource(timeout);
            var task = DnsClient.Default.ResolveAsync(DomainName.Parse(domain), RecordType.Mx, token: tokenSource.Token);
            try
            {
                task.Wait();
            }
            catch (AggregateException)
            {
                //timeout
                found = false;
                return BlankResult;
            }

            if (task.Result == null)
            {
                found = false;
                return BlankResult;
            }

            var records = task.Result.AnswerRecords.OfType<MxRecord>();
            found = records.Any();
            return records.Select(r => r.ExchangeDomainName.ToString());
        }

        public static bool GetIpAddresses(string domain, out IPAddress[] addresses, int timeout = 60*1000)
        {
            var tokenSource = new CancellationTokenSource(timeout);
            var task = DnsClient.Default.ResolveAsync(DomainName.Parse(domain), token: tokenSource.Token);

            try
            {
                task.Wait();
            }
            catch (AggregateException)
            {
                //timeout
                addresses = null;
                return false;
            }

            if (task.Result == null)
            {
                addresses = null;
                return false;
            }

            //Debug.Assert(task.IsCompleted);
            var records = task.Result.AnswerRecords.OfType<ARecord>();
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