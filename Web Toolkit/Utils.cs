using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{
    public static class Utils
    {
        //Adapted from Noah Heldman's work at http://stackoverflow.com/a/10407992/17027
        public static bool GetClientIpAddress(HttpRequestBase request, out string remote)
        {
            try
            {
                var userHostAddress = request.UserHostAddress;

                // Attempt to parse.  If it fails, we catch below and return "0.0.0.0"
                // Could use TryParse instead, but I wanted to catch all exceptions
                IPAddress.Parse(userHostAddress);

                var xForwardedFor = request.ServerVariables.AllKeys.Contains("HTTP_X_FORWARDED_FOR") ? request.ServerVariables["HTTP_X_FORWARDED_FOR"] :
                    request.ServerVariables.AllKeys.Contains("X_FORWARDED_FOR") ? request.ServerVariables["X_FORWARDED_FOR"] : "";

                if (string.IsNullOrWhiteSpace(xForwardedFor))
                {
                    remote = userHostAddress;
                    return true;
                }

                // Get a list of public ip addresses in the X_FORWARDED_FOR variable
                var publicForwardingIps = xForwardedFor.Split(',').Where(ip => !IsPrivateIpAddress(ip)).ToList();

                // If we found any, return the last one, otherwise return the user host address
                remote = publicForwardingIps.Any() ? publicForwardingIps.Last() : userHostAddress;
                return true;
            }
            catch (Exception)
            {
                // Always return all zeroes for any failure
                remote = "0.0.0.0";
                return false;
            }
        }

        //Note: Does not support IPv6
        private static bool IsPrivateIpAddress(string ipAddress)
        {
            // http://en.wikipedia.org/wiki/Private_network
            // Private IP Addresses are: 
            //  24-bit block: 10.0.0.0 through 10.255.255.255
            //  20-bit block: 172.16.0.0 through 172.31.255.255
            //  16-bit block: 192.168.0.0 through 192.168.255.255
            //  Link-local addresses: 169.254.0.0 through 169.254.255.255 (http://en.wikipedia.org/wiki/Link-local_address)

            var ip = IPAddress.Parse(ipAddress);
            var octets = ip.GetAddressBytes();

            var is24BitBlock = octets[0] == 10;
            if (is24BitBlock) return true; // Return to prevent further processing

            var is20BitBlock = octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31;
            if (is20BitBlock) return true; // Return to prevent further processing

            var is16BitBlock = octets[0] == 192 && octets[1] == 168;
            if (is16BitBlock) return true; // Return to prevent further processing

            var isLinkLocalAddress = octets[0] == 169 && octets[1] == 254;
            return isLinkLocalAddress;
        }
    }
}
