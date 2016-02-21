using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public string[] PreservedParameters;
    }

    static public class Seo
    {
        public enum QueryStringBehavior
        {
            StripAll,
            KeepActionParameters,
            KeepAll
        }

        private static readonly ConcurrentDictionary<ulong, CachedMethod> MethodCache = new ConcurrentDictionary<ulong, CachedMethod>();

        public static void SeoRedirect(this Controller controller, string[] alsoPreserveQueryStringKeys, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            SeoRedirect(controller, QueryStringBehavior.KeepActionParameters, alsoPreserveQueryStringKeys, filePath, lineNumber);
        }

        public static void SeoRedirect(this Controller controller, QueryStringBehavior stripQueryStrings = QueryStringBehavior.KeepActionParameters, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            SeoRedirect(controller, stripQueryStrings, null, filePath, lineNumber);
        }

        private static void SeoRedirect(Controller controller, QueryStringBehavior stripQueryStrings, string[] additionalPreservedKeys, string filePath, int lineNumber)
        {
            var key = CityHash.CityHash.CityHash64(filePath, (ulong) lineNumber);
            CachedMethod callingMethod;
            if (!MethodCache.TryGetValue(key, out callingMethod))
            {
                var method = new StackFrame(2).GetMethod();
                callingMethod = new CachedMethod
                {
                    Action = method.Name,
                    Controller = method.DeclaringType.Name.Remove(method.DeclaringType.Name.Length - "Controller".Length),
                    IsIndex = method.Name == "Index"
                };

                if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
                {
                    //Optimization: if no explicitly preserved query strings and no action parameters, avoid creating HashSet and strip all
                    if ((additionalPreservedKeys == null || additionalPreservedKeys.Length == 0) && method.GetParameters().Length == 0)
                    {
                        //This won't actually persist anywhere because it's only set once during the reflection phase and not included in the cache entry
                        //we're relying on checking if HashSet == null in DetermineSeoRedirect
#if DEBUG
                        //I'd leave it in here and rely on the compiler to strip it out, but...
                        stripQueryStrings = QueryStringBehavior.StripAll;
#endif
                    }
                    else
                    {
                        int i = 0;
                        bool skippedId = false;
                        bool routeHasId = controller.RouteData.Values.ContainsKey("id");
                        bool requestHasId = HttpContext.Current.Request.QueryString.Keys.Cast<string>().Any(queryKey => queryKey == "id");
                        callingMethod.PreservedParameters = new string[(method.GetParameters().Length + (additionalPreservedKeys?.Length ?? 0))];
                        foreach (var preserved in method.GetParameters())
                        {
                            //Optimization: remove the 'id' parameter if it's determined by the route, potentially saving on HashSet lookup entirely
                            if (!skippedId && routeHasId && !requestHasId && preserved.Name == "id")
                            {
                                //The parameter id is part of the route and not obtained via query string parameters
                                if (callingMethod.PreservedParameters.Length == 1)
                                {
                                    //Bypass everything, no need for parameter preservation
                                    //This won't actually persist anywhere because it's only set once during the reflection phase and not included in the cache entry
                                    //we're relying on checking if HashSet == null in DetermineSeoRedirect
#if DEBUG
                                    //I'd leave it in here and rely on the compiler to strip it out, but...
                                    stripQueryStrings = QueryStringBehavior.StripAll;
#endif
                                    callingMethod.PreservedParameters = null;
                                    break;
                                }

                                skippedId = true;
                                var newPreserved = new string[callingMethod.PreservedParameters.Length - 1];
                                Array.Copy(callingMethod.PreservedParameters, 0, newPreserved, 0, i);
                                callingMethod.PreservedParameters = newPreserved;
                                continue;
                            }
                            callingMethod.PreservedParameters[i++] = preserved.Name;
                        }
                        if (additionalPreservedKeys != null)
                        {
                            foreach (var preserved in additionalPreservedKeys)
                            {
                                callingMethod.PreservedParameters[i++] = preserved;
                            }
                        }

                        if (callingMethod.PreservedParameters != null && callingMethod.PreservedParameters.Length > 1)
                        {
                            Array.Sort(callingMethod.PreservedParameters);
                        }
                    }
                }

                MethodCache.TryAdd(key, callingMethod);
            }

            string destination;
            if (DetermineSeoRedirect(controller, callingMethod, stripQueryStrings, out destination))
            {
                controller.Response.RedirectPermanent(destination);
            }
        }

        private static bool DetermineSeoRedirect(Controller controller, CachedMethod method, QueryStringBehavior stripQueryStrings, out string destination)
        {
            bool redirect = false;
            string currentAction = (string)controller.RouteData.Values["action"];
            string currentController = (string)controller.RouteData.Values["controller"];

            //tentatively....
            //note: not using a string builder because the assumption is that most requests are correct
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
            if (stripQueryStrings == QueryStringBehavior.StripAll || method.PreservedParameters == null)
            {
                redirect = redirect || HttpContext.Current.Request.QueryString.Count > 0;
            }
            else if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
            {
                if (HttpContext.Current.Request.QueryString.AllKeys.Any(k => Array.BinarySearch(method.PreservedParameters, k) < 0))
                {
                    redirect = true;

                    var i = 0;
                    StringBuilder qsBuilder = null;
                    foreach (var key in HttpContext.Current.Request.QueryString.AllKeys.Where(k => Array.BinarySearch(method.PreservedParameters, k) >= 0))
                    {
                        var value = HttpContext.Current.Request.QueryString[key];
                        qsBuilder = qsBuilder ?? new StringBuilder();
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
            else //QueryStringBehavior.KeepAll
            {
                destination += HttpContext.Current.Request.Url.Query;
            }

            return redirect;
        }
    }
}
