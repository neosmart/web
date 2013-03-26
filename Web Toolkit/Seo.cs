using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        public static void SeoRedirect(Controller controller, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            string key = string.Format("{0}:{1}", filePath, lineNumber);
            CachedMethod lastMethod;
            if (!MethodCache.TryGetValue(key, out lastMethod))
            {
                var method = new StackFrame(1).GetMethod();
                lastMethod = new CachedMethod
                    {
                        Action = method.Name,
                        Controller = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length),
                        IsIndex =  method.Name == "Index"
                    };
                MethodCache.TryAdd(key, lastMethod);
            }

            string destination;
            if (DetermineSeoRedirect(controller, lastMethod, out destination))
            {
                controller.Response.RedirectPermanent(destination);
            }
        }

        private static bool DetermineSeoRedirect(Controller controller, CachedMethod method, out string destination)
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
                destination = string.Format("{0}{1}", destination.TrimEnd(new[] { '/' }), method.IsIndex ? "/" : "");
            }

            //No Index in link
            if (method.IsIndex && destination.EndsWith("/Index/"))
            {
                redirect = true;
                const string search = "Index/";
                destination = destination.Remove(destination.Length - search.Length);
            }

            return redirect;
        }
    }
}
