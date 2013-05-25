using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    static public class DnsLookup
    {
        [DllImport("dnsapi", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        private static extern int DnsQuery([MarshalAs(UnmanagedType.VBByRefStr)] ref string lpstrName, RecordType wType,
                                           QueryOptions options, int pExtra, ref IntPtr ppQueryResultsSet, int pReserved);

        [DllImport("dnsapi", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void DnsRecordListFree(IntPtr pRecordList, int freeType);

        public static IEnumerable<string> GetMXRecords(string domain, out bool found)
        {
            var records = new List<string>();

            GetDnsRecords(RecordType.DNS_TYPE_MX, domain, delegate(IntPtr ptr)
                {
                    MXRecord mxRecord;
                    for (IntPtr nextRecord = ptr; nextRecord != IntPtr.Zero; nextRecord = mxRecord.pNext)
                    {
                        mxRecord = (MXRecord) Marshal.PtrToStructure(nextRecord, typeof (MXRecord));
                        if (mxRecord.wType == (short) RecordType.DNS_TYPE_MX)
                        {
                            records.Add(Marshal.PtrToStringAuto(mxRecord.pNameExchange));
                        }
                    }
                });

            found = records.Any();
            return records;
        }

        private static void GetDnsRecords(RecordType recordType, string domain, Action<IntPtr> handler)
        {
            IntPtr queryResults = IntPtr.Zero;
            if (DnsQuery(ref domain, recordType, /*QueryOptions.DNS_QUERY_BYPASS_CACHE*/ QueryOptions.DNS_QUERY_STANDARD, 0, ref queryResults, 0) != 0)
            {
                return;
            }

            handler(queryResults);

            DnsRecordListFree(queryResults, 0);
        }

        private enum QueryOptions
        {
            DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE = 1,
            DNS_QUERY_BYPASS_CACHE = 8,
            DNS_QUERY_DONT_RESET_TTL_VALUES = 0x100000,
            DNS_QUERY_NO_HOSTS_FILE = 0x40,
            DNS_QUERY_NO_LOCAL_NAME = 0x20,
            DNS_QUERY_NO_NETBT = 0x80,
            DNS_QUERY_NO_RECURSION = 4,
            DNS_QUERY_NO_WIRE_QUERY = 0x10,
            DNS_QUERY_RESERVED = -16777216,
            DNS_QUERY_RETURN_MESSAGE = 0x200,
            DNS_QUERY_STANDARD = 0,
            DNS_QUERY_TREAT_AS_FQDN = 0x1000,
            DNS_QUERY_USE_TCP_ONLY = 2,
            DNS_QUERY_WIRE_ONLY = 0x100
        }

        private enum RecordType
        {
            DNS_TYPE_A = 0x0001,
            DNS_TYPE_NS = 0x0002,
            DNS_TYPE_MD = 0x0003,
            DNS_TYPE_MF = 0x0004,
            DNS_TYPE_CNAME = 0x0005,
            DNS_TYPE_SOA = 0x0006,
            DNS_TYPE_MB = 0x0007,
            DNS_TYPE_MG = 0x0008,
            DNS_TYPE_MR = 0x0009,
            DNS_TYPE_NULL = 0x000A,
            DNS_TYPE_WKS = 0x000B,
            DNS_TYPE_PTR = 0x000C,
            DNS_TYPE_HINFO = 0x000D,
            DNS_TYPE_MINFO = 0x000E,
            DNS_TYPE_MX = 0x000F,
            DNS_TYPE_TEXT = 0x0010,
            DNS_TYPE_RP = 0x0011,
            DNS_TYPE_AFSDB = 0x0012,
            DNS_TYPE_X25 = 0x0013,
            DNS_TYPE_ISDN = 0x0014,
            DNS_TYPE_RT = 0x0015,
            DNS_TYPE_NSAP = 0x0016,
            DNS_TYPE_NSAPPTR = 0x0017,
            DNS_TYPE_SIG = 0x0018,
            DNS_TYPE_KEY = 0x0019,
            DNS_TYPE_PX = 0x001A,
            DNS_TYPE_GPOS = 0x001B,
            DNS_TYPE_AAAA = 0x001C,
            DNS_TYPE_LOC = 0x001D,
            DNS_TYPE_NXT = 0x001E,
            DNS_TYPE_EID = 0x001F,
            DNS_TYPE_NIMLOC = 0x0020,
            DNS_TYPE_SRV = 0x0021,
            DNS_TYPE_ATMA = 0x0022,
            DNS_TYPE_NAPTR = 0x0023,
            DNS_TYPE_KX = 0x0024,
            DNS_TYPE_CERT = 0x0025,
            DNS_TYPE_A6 = 0x0026,
            DNS_TYPE_DNAME = 0x0027,
            DNS_TYPE_SINK = 0x0028,
            DNS_TYPE_OPT = 0x0029,
            DNS_TYPE_DS = 0x002B,
            DNS_TYPE_RRSIG = 0x002E,
            DNS_TYPE_NSEC = 0x002F,
            DNS_TYPE_DNSKEY = 0x0030,
            DNS_TYPE_DHCID = 0x0031,
            DNS_TYPE_UINFO = 0x0064,
            DNS_TYPE_UID = 0x0065,
            DNS_TYPE_GID = 0x0066,
            DNS_TYPE_UNSPEC = 0x0067,
            DNS_TYPE_ADDRS = 0x00F8,
            DNS_TYPE_TKEY = 0x00F9,
            DNS_TYPE_TSIG = 0x00FA,
            DNS_TYPE_IXFR = 0x00FB,
            DNS_TYPE_AXFR = 0x00FC,
            DNS_TYPE_MAILB = 0x00FD,
            DNS_TYPE_MAILA = 0x00FE,
            DNS_TYPE_ALL = 0x00FF,
            DNS_TYPE_ANY = 0x00FF,
            DNS_TYPE_WINS = 0xFF01,
            DNS_TYPE_WINSR = 0xFF02,
            DNS_TYPE_NBSTAT = DNS_TYPE_WINSR
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MXRecord
        {
            public IntPtr pNext;
            public string pName;
            public short wType;
            public short wDataLength;
            public int flags;
            public int dwTtl;
            public int dwReserved;
            public IntPtr pNameExchange;
            public short wPreference;
            public short Pad;
        }
    }
}