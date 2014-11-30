using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{
    internal abstract class NameObjectCollection : NameObjectCollectionBase
    {
        public object GetAtIndex(int index)
        {
            return BaseGet(index);
        }
    }

    public static class NameObjectColletionBaseExtensions
    {
        public static bool TryGetValue<T>(this NameObjectCollectionBase dictionary, string key, out T value)
        {
            for (int i = 0; i < dictionary.Count; ++i)
            {
                if (dictionary.Keys[i] == key)
                {
                    value = (T)((NameObjectCollection)dictionary).GetAtIndex(i);
                    return true;
                }
            }

            value = default(T);
            return false;
        }

        public static T SafeLookup<T>(this NameObjectCollectionBase dictionary, string key, T ifNotFound = default(T))
        {
            T value;
            return dictionary.TryGetValue(key, out value) ? value : ifNotFound;
        }
    }

    public static class NameValueCollectionExtensions
    {
        public static bool TryGetValue(this NameValueCollection dictionary, string key, out string value)
        {
            return dictionary.TryGetValue<string>(key, out value);
        }

        public static string SafeLookup(this NameValueCollection dictionary, string key, string ifNotFound = "")
        {
            return dictionary.SafeLookup<string>(key, ifNotFound);
        }
    }

    public static class HttpCookieCollectionExtensions
    {
        public static bool TryGetValue(this HttpCookieCollection dictionary, string key, out HttpCookie value)
        {
            return dictionary.TryGetValue<HttpCookie>(key, out value);
        }

        public static HttpCookie SafeLookup(this HttpCookieCollection dictionary, string key, HttpCookie ifNotFound = default(HttpCookie))
        {
            return dictionary.SafeLookup<HttpCookie>(key, ifNotFound);
        }
    }
}
