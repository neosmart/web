using NeoSmart.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{
    public static class Security
    {
        public static readonly SortedSet<string> ValidReferers = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase)
        {
            null,
            ""
        };

        public static bool ValidateReferer(string referer, bool nothrow = false)
        {
            if (!ValidReferers.Contains(referer))
            {
                if (nothrow)
                {
                    return false;
                }
                throw new InvalidRefererException(referer);
            }

            return true;
        }

        public static bool ValidateReferer(this HttpRequestBase request, bool nothrow = false)
        {
            return ValidateReferer(request.UrlReferrer?.Host, nothrow);
        }
    }

    public class InvalidRefererException : HttpException
    {
        public readonly string Referer;

        public InvalidRefererException(string referer)
            : base(403, "Access to this resource is restricted!")
        {
            Referer = referer;
        }
    }
}
