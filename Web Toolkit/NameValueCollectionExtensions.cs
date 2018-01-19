using System.Collections.Specialized;

namespace NeoSmart.Web
{
    public static class NameValueCollectionExtensions
    {
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