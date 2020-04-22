using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Routing;
using System;

namespace NeoSmart.Web
{
    public static class UrlExtensions
    {
        public static string Content(this UrlHelper urlHelper, HttpRequest request, string contentPath, bool absolute = false)
        {
            var path = urlHelper.Content(contentPath);
            if (!absolute)
            {
                return contentPath;
            }

            var uri = new Uri(request.GetDisplayUrl() + path);
            return uri.AbsoluteUri;
        }
    }
}
