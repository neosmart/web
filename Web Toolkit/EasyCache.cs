using System;
using System.Web;

namespace NeoSmart.Web
{
    static public class EasyCache
    {
        static public void Cache(this HttpResponseBase response, TimeSpan expiresIn)
        {
            response.Cache.SetOmitVaryStar(true);
            response.Cache.SetExpires(DateTime.UtcNow + expiresIn);
            response.Cache.SetCacheability(HttpCacheability.Public);
        }

        public static void NoCache(this HttpResponseBase response)
        {
            response.Cache.SetOmitVaryStar(true);
            response.Cache.SetExpires(DateTime.MinValue);
            response.Cache.SetCacheability(HttpCacheability.NoCache);
        }
    }
}
