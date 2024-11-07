using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace NeoSmart.Web
{
    public static class Security
    {
        public static readonly SortedSet<string?> ValidReferers = new(StringComparer.OrdinalIgnoreCase)
        {
            null,
            ""
        };

        public static bool ValidateReferer(string? referer)
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
