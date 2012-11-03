using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web;
using System.Web.Mvc;

namespace NeoSmart.Web
{
    public class Seo
    {
        private static readonly Dictionary<string, string> MethodCache = new Dictionary<string, string>();

        public static RedirectResult CaseSensitiveRedirect(Controller controller, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            string key = string.Format("{0}:{1}", filePath, lineNumber);
            string correctPath;
            if (!MethodCache.TryGetValue(key, out correctPath))
            {
                var lastMethod = new StackFrame(1).GetMethod();
                CaseSensitiveRedirect(controller, lastMethod, out correctPath);
                MethodCache.Add(key, correctPath);
            }

            if (HttpContext.Current.Request.Url.AbsolutePath != correctPath)
            {
                return new RedirectResult(correctPath, true);
            }

            return null;
        }

        public static bool CaseSensitiveRedirect(Controller controller, MethodBase method, out string redirectPath)
        {
            string currentAction = (string)controller.RouteData.Values["action"];
            string currentController = (string)controller.RouteData.Values["controller"];

            string realAction = method.Name;
            string realController = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length);

            //Case Redirect
            if (currentAction != realAction || currentController != realController)
            {
                redirectPath = HttpContext.Current.Request.Url.AbsolutePath.Replace(currentAction, realAction).Replace(currentController, realController);
                return true;
            }

            //Trailing-backslash configuration
            bool isIndex = realAction == "Index";
            if ((isIndex && !HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")) || (!isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")))
            {
                redirectPath = string.Format("{0}{1}", HttpContext.Current.Request.Url.AbsolutePath.TrimEnd(new[] { '/' }), isIndex ? "/" : "");
                return true;
            }

            //No Index in link
            if (isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/Index/"))
            {
                const string search = "Index/";
                redirectPath = HttpContext.Current.Request.Url.AbsolutePath.Substring(0, HttpContext.Current.Request.Url.AbsolutePath.Length - search.Length);
                return true;
            }

            redirectPath = HttpContext.Current.Request.Url.AbsolutePath;
            return false;
        }
    }
}
