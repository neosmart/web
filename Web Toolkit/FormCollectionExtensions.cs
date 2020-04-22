using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace NeoSmart.Web
{
    public static class FormCollectionExtensions
    {
        public static bool TryGetSingleValue(this IFormCollection collection, string key, out string value)
        {
            StringValues values;
            if (collection.TryGetValue(key, out values))
            {
                value = values[0];
                return true;
            }
            value = null;
            return false;
        }

        public static string SafeLookup(this IFormCollection collection, string key)
        {
            if (collection.TryGetSingleValue(key, out var value))
            {
                return value;
            }
            return "";
        }

        public static bool TryGetSingleValue(this IQueryCollection collection, string key, out string value)
        {
            StringValues values;
            if (collection.TryGetValue(key, out values))
            {
                value = values[0];
                return true;
            }
            value = null;
            return false;
        }

        public static string SafeLookup(this IQueryCollection collection, string key)
        {
            if (collection.TryGetSingleValue(key, out var value))
            {
                return value;
            }
            return "";
        }
    }
}
