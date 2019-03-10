using Microsoft.AspNetCore.Http;
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

        public static bool ValidateReferer(string referer)
        {
            if (!ValidReferers.Contains(referer))
            {
                return false;
            }

            return true;
        }

        public static bool ValidateReferer(this HttpRequest request)
        {
            var referer = request.GetTypedHeaders().Referer;
            return ValidateReferer(referer?.Host);
        }
    }
}
