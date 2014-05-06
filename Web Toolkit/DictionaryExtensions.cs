using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public static class DictionaryExtensions
    {
        public static TValue SafeLookup<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue ifNotFound)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : ifNotFound;
        }
    }
}
