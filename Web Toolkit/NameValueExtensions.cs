using System;
using System.Collections.Specialized;
using System.Linq;

namespace NeoSmart.Web
{
    public static class NameValueExtensions
    {
        public static bool TryGetValue(this NameValueCollection dictionary, string key, out string value)
        {
            if (dictionary.AllKeys.Contains(key))
            {
                value = dictionary.Get(key);
                return true;
            }

            value = null;
            return false;
        }

        public static string SafeLookup(this NameValueCollection dictionary, string key, string ifNotFound = "")
        {
            return dictionary.TryGetValue(key, out string value) ? value : ifNotFound;
        }

        public static bool Contains(this NameObjectCollectionBase.KeysCollection keys, string key)
        {
            for (int i = 0; i < keys.Count; ++i)
            {
                var item = keys.Get(i);
                if (item == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
