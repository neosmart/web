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
        public static void CaseSensitiveRedirect(Controller controller, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            string key = string.Format("{0}:{1}", filePath, lineNumber);
            MethodBase lastMethod;
            if (!MethodCache.TryGetValue(key, out lastMethod))
            {
                lastMethod = new StackFrame(1).GetMethod();
                MethodCache.Add(key, lastMethod);
            }

            string destination;
            if (CaseSensitiveRedirect(controller, lastMethod, out destination))
            {
                controller.Response.RedirectPermanent(destination);
            }
        }

        private static bool CaseSensitiveRedirect(Controller controller, MethodBase method, out string destination)
        {
            string currentAction = (string)controller.RouteData.Values["action"];
            string currentController = (string)controller.RouteData.Values["controller"];

            string realAction = method.Name;
            string realController = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length);

            //Case Redirect
            if (currentAction != realAction || currentController != realController)
            {
                destination = HttpContext.Current.Request.Url.AbsolutePath.Replace(currentAction, realAction).Replace(currentController, realController);
                return true;
            }

            //Trailing-backslash configuration
            bool isIndex = realAction == "Index";
            if ((isIndex && !HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")) || (!isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/")))
            {
                destination = string.Format("{0}{1}", HttpContext.Current.Request.Url.AbsolutePath.TrimEnd(new[] { '/' }), isIndex ? "/" : "");
                return true;
            }

            //No Index in link
            if (isIndex && HttpContext.Current.Request.Url.AbsolutePath.EndsWith("/Index/"))
            {
                const string search = "Index/";
                destination = HttpContext.Current.Request.Url.AbsolutePath.Substring(0, HttpContext.Current.Request.Url.AbsolutePath.Length - search.Length);
                return true;
            }

            destination = string.Empty;
            return false;
        }
    }
}
