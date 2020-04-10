using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{
    public static class Utils
    {
        public static bool GetClientIpAddress(HttpRequest request, out string remote)
        {
            try
            {
                remote = string.Empty;
                _ = request.Headers.TryGetValue("HTTP_X_FORWARDED_FOR", out var xForwardedFor) ||
                    request.Headers.TryGetValue("X_FORWARDED_FOR", out xForwardedFor) ||
                    request.Headers.TryGetValue("HTTP-X-FORWARDED-FOR", out xForwardedFor) ||
                    request.Headers.TryGetValue("X-FORWARDED-FOR", out xForwardedFor);

                if (xForwardedFor.Count > 0)
                {
                    //Get a list of public ip addresses in the X_FORWARDED_FOR variable
                    var publicForwardingIps = xForwardedFor.Where(ip => !IsPrivateIpAddress(ip)).ToList();

                    //If we found any, return the last one, otherwise return the user host address
                    if (publicForwardingIps.Any())
                    {
                        remote = publicForwardingIps.Last();
                        return true;
                    }
                }

                // Use provided remote address, if available
                var connectionFeature = request.HttpContext.Features.Get<HttpConnectionFeature>();
                var userHostAddress = connectionFeature?.RemoteIpAddress?.ToString() ?? "";
                if (!IPAddress.TryParse(userHostAddress, out _))
                {
                    remote = "0.0.0.0";
                    return false;
                }

                remote = userHostAddress;
                return true;
            }
            catch (Exception)
            {
                //Always return all zeroes for any failure
                remote = "0.0.0.0";
                return false;
            }
        }

        private static bool IsPrivateIpAddress(string ipAddress)
        {
            //http://en.wikipedia.org/wiki/Private_network
            //Private IP Addresses are:
            //  24-bit block: 10.0.0.0 through 10.255.255.255
            //  20-bit block: 172.16.0.0 through 172.31.255.255
            //  16-bit block: 192.168.0.0 through 192.168.255.255
            //  Link-local addresses: 169.254.0.0 through 169.254.255.255 (http://en.wikipedia.org/wiki/Link-local_address)

            IPAddress ip;
            if (IPAddress.TryParse(ipAddress, out ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    //Assume all IPv6 addresses are public-facing (no NATing)
                    return false;
                }

                if (ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    //Unknown/malformed "IP" address, cant' be a web-facing IP
                    return true; //nothing we can do about this
                }
            }
            else
            {
                //Unknown/malformed "IP" address, cant' be a web-facing IP
                return true; //nothing we can do about this
            }

            var octets = ip.GetAddressBytes();

            var is24BitBlock = octets[0] == 10;
            if (is24BitBlock) return true; //Return to prevent further processing

            var is20BitBlock = octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31;
            if (is20BitBlock) return true; //Return to prevent further processing

            var is16BitBlock = octets[0] == 192 && octets[1] == 168;
            if (is16BitBlock) return true; //Return to prevent further processing

            var isLinkLocalAddress = octets[0] == 169 && octets[1] == 254;
            return isLinkLocalAddress;
        }

        public static string EncodeStringDictionary(IDictionary<string, string> dictionary)
        {
            var sb = new StringBuilder();

            foreach (var kv in dictionary)
            {
                sb.AppendFormat("{0}={1}&", Uri.EscapeDataString(kv.Key), Uri.EscapeDataString(kv.Value));
            }

            //Trim trailing &
            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
            }

            return sb.ToString();
        }

        public static string ByteToHex(byte[] bytes)
        {
            Span<char> c = stackalloc char[bytes.Length << 1];

            byte b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = ((byte)(bytes[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = ((byte)(bytes[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }

            return c.ToString();
        }
    }
}
