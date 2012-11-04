using System;
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
    public class Seo
    {
        private static readonly Dictionary<string, MethodBase> MethodCache = new Dictionary<string, MethodBase>();
        public static RedirectResult CaseSensitiveRedirect(Controller controller, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            string key = string.Format("{0}:{1}", filePath, lineNumber);
            MethodBase lastMethod;
            if (!MethodCache.TryGetValue(key, out lastMethod))
            {
                lastMethod = new StackFrame(1).GetMethod();
                MethodCache.Add(key, lastMethod);
            }
            return CaseSensitiveRedirect(controller, lastMethod);
        }

        private static RedirectResult CaseSensitiveRedirect(Controller controller, MethodBase method)
        {
            string currentAction = (string)controller.RouteData.Values["action"];
            string currentController = (string)controller.RouteData.Values["controller"];

            string realAction = method.Name;
            string realController = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length);

            //Case Redirect
            if (currentAction != realAction || currentController != realController)
            {
                return new RedirectResult(HttpContext.Current.Request.Url.AbsolutePath.Replace(currentAction, realAction).Replace(currentController, realController), true);
            }

            //Trailing-backslash configuration
            bool isIndex = realAction == "Index";
            if ((isIndex && !HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")) || (!isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")))
            {
                var redirect = string.Format("{0}{1}", HttpContext.Current.Request.Url.AbsolutePath.TrimEnd(new[] { '/' }), isIndex ? "/" : "");
                return new RedirectResult(redirect, true);
            }

            //No Index in link
            if (isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/Index/"))
            {
                const string search = "Index/";
                var redirect = HttpContext.Current.Request.Url.AbsolutePath.Substring(0, HttpContext.Current.Request.Url.AbsolutePath.Length - search.Length);
                return new RedirectResult(redirect, true);
            }

            return null;
        }
    }
}
