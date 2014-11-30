using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public static class NameValueCollectionExtensions
    {
        public static bool TryGetValue(this System.Collections.Specialized.NameValueCollection dictionary, string key, out string value)
        {
            if (dictionary.AllKeys.Contains(key))
            {
                value = dictionary.Get(key);
                return true;
            }

            value = null;
            return false;
        }

        public static string SafeLookup(this System.Collections.Specialized.NameValueCollection dictionary, string key, string ifNotFound = "")
        {
            string value;
            return dictionary.TryGetValue(key, out value) ? value : ifNotFound;
        }
    }
}
