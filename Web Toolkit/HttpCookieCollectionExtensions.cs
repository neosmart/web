using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{

    public static class HttpCookieCollectionExtensions
    {
#if false
        public static bool TryGetValue(this HttpCookieCollection dictionary, string key, out HttpCookie value)
        {
            if (dictionary.AllKeys.Contains(key))
            {
                value = dictionary.Get(key);
                return true;
            }

            value = null;
            return false;
        }

        public static HttpCookie SafeLookup(this HttpCookieCollection dictionary, string key, HttpCookie ifNotFound = default(HttpCookie))
        {
            return dictionary.TryGetValue(key, out var value) ? value : ifNotFound;
        }
#endif
    }
}
