using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace NeoSmart.Web
{
    class CachedMethod
    {
        public string Controller;
        public string Action;
        public bool IsIndex;
    }

    public class Seo
    {
        private static readonly ConcurrentDictionary<string, CachedMethod> MethodCache = new ConcurrentDictionary<string, CachedMethod>();

        public static void SeoRedirect(Controller controller, string[] preserveQueryStrings, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            SeoRedirect(controller, false, preserveQueryStrings, filePath, lineNumber);
        }

        public static void SeoRedirect(Controller controller, bool preserveAllQueryStrings = false, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            SeoRedirect(controller, preserveAllQueryStrings, null, filePath, lineNumber);
        }

        private static void SeoRedirect(Controller controller, bool preserveAllQueryStrings, string[] preservedQueryStrings, string filePath, int lineNumber)
        {
            string key = string.Format("{0}:{1}", filePath, lineNumber);
            CachedMethod lastMethod;
            if (!MethodCache.TryGetValue(key, out lastMethod))
            {
                var method = new StackFrame(2).GetMethod();
                lastMethod = new CachedMethod
                {
                    Action = method.Name,
                    Controller = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length),
                    IsIndex = method.Name == "Index"
                };
                MethodCache.TryAdd(key, lastMethod);
            }

            string destination;
            if (DetermineSeoRedirect(controller, lastMethod, preserveAllQueryStrings, preservedQueryStrings, out destination))
            {
                controller.Response.RedirectPermanent(destination);
            }
        }

        private static bool DetermineSeoRedirect(Controller controller, CachedMethod method, bool preserveAllQueryStrings, string[] preservedQueryStrings, out string destination)
        {
            bool redirect = false;
            string currentAction = (string)controller.RouteData.Values["action"];
            string currentController = (string)controller.RouteData.Values["controller"];

            //tentatively....
            destination = HttpContext.Current.Request.Url.AbsolutePath;

            //Case-based redirect
            if (currentAction != method.Action)
            {
                redirect = true;
                destination = destination.Replace(currentAction, method.Action);
            }

            //Case-based redirect
            if (currentController != method.Controller)
            {
                redirect = true;
                destination = destination.Replace(currentController, method.Controller);
            }

            //Trailing-backslash configuration (this is the simplification of the NOT XNOR)
            if ((method.IsIndex != destination.EndsWith("/")))
            {
                redirect = true;
                destination = string.Format("{0}{1}", destination.TrimEnd('/'), method.IsIndex ? "/" : "");
            }

            //No Index in link
            if (method.IsIndex && destination.EndsWith("/Index/"))
            {
                redirect = true;
                const string search = "Index/";
                destination = destination.Remove(destination.Length - search.Length);
            }

            //Query strings
            if (!preserveAllQueryStrings)
            {
                if (HttpContext.Current.Request.QueryString.AllKeys.Any(
                        queryString => !preservedQueryStrings.Contains(queryString)))
                {
                    redirect = true;

                    var i = 0;
                    StringBuilder qsBuilder = null;
                    foreach (var key in HttpContext.Current.Request.QueryString.AllKeys.Where(key => preservedQueryStrings.Contains(key)))
                    {
                        var value = HttpContext.Current.Request.QueryString[key];
                        qsBuilder = i == 0 ? new StringBuilder() : qsBuilder;
                        qsBuilder.AppendFormat("{0}{1}{2}{3}", i == 0 ? '?' : '&', key,
                            string.IsNullOrEmpty(value) ? "" : "=",
                            string.IsNullOrEmpty(value) ? "" : HttpUtility.UrlEncode(value));

                        ++i;
                    }

                    if (qsBuilder != null)
                    {
                        destination += qsBuilder.ToString();
                    }
                }
            }

            return redirect;
        }
    }
}
