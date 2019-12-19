using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using NeoSmart.ExtensionMethods;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using HttpUtility = System.Web.HttpUtility;

namespace NeoSmart.Web
{
    static public class Seo
    {
        public static string[] PreservedQueryStrings = new string[]
        {
            "gclid",
            "utm_campaign",
            "utm_content",
            "utm_medium",
            "utm_nooverride",
            "utm_source",
            "utm_term",
        };

        public enum QueryStringBehavior
        {
            StripAll,
            KeepActionParameters,
            KeepAll
        }

        private static readonly ConcurrentDictionary<ulong, CachedMethod> MethodCache = new ConcurrentDictionary<ulong, CachedMethod>();

        public static void RobotsTag(this Controller controller, string value, string botName = null)
        {
            controller.Response.Headers.Add("X-Robots-Tag", string.Format("{0}{1}{2}", botName ?? string.Empty, botName != null ? ": " : string.Empty, value));
        }

        public static void NoIndex(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "noindex", botName);
        }

        public static void NoFollow(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "nofollow", botName);
        }

        public static void NoIndexNoFollow(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "noindex, nofollow", botName);
        }

        public static void NoArchive(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "noarchive", botName);
        }

        public static void NoSnippet(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "nosnippet", botName);
        }

        public static void NoTranslate(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "notranslate", botName);
        }

        public static void NoImageIndex(this Controller controller, string botName = null)
        {
            RobotsTag(controller, "noimageindex", botName);
        }

        public static void UnavailableAfter(this Controller controller, DateTime max, string botName = null)
        {
            //Google can't make up its mind. First it asks for RFC 850 then in an example it uses something else!
            //https://developers.google.com/webmasters/control-crawl-index/docs/robots_meta_tag
            //The globally-accepted "web standard" http format is RFC1123. https://www.hackcraft.net/web/datetime/#rfc850
            RobotsTag(controller, $"unavailable_after: {max:R}", botName);
        }

        public static void SeoRedirect(this Controller controller, HttpRequest request, string[] alsoPreserveQueryStringKeys = null, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (alsoPreserveQueryStringKeys == null)
            {
                alsoPreserveQueryStringKeys = PreservedQueryStrings;
            }

            var key = NeoSmart.Hashing.XXHash.XXHash64.Hash((ulong)lineNumber, filePath);
            if (MethodCache.TryGetValue(key, out var cachedMethod))
            {
                string destination;
                if (DetermineSeoRedirect(controller, request, cachedMethod, QueryStringBehavior.KeepActionParameters, out destination))
                {
                    controller.Response.Redirect(destination, true);
                }
                return;
            }

            var callingMethod = new StackFrame(1).GetMethod();
            SeoRedirect(controller, request, QueryStringBehavior.KeepActionParameters, alsoPreserveQueryStringKeys, callingMethod, key);
        }

        public static void SeoRedirect(this Controller controller, HttpRequest request, QueryStringBehavior stripQueryStrings, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var key = NeoSmart.Hashing.XXHash.XXHash64.Hash((ulong)lineNumber, filePath);
            if (MethodCache.TryGetValue(key, out var cachedMethod))
            {
                string destination;
                if (DetermineSeoRedirect(controller, request, cachedMethod, stripQueryStrings, out destination))
                {
                    controller.Response.Redirect(destination, true);
                }
                return;
            }

            var callingMethod = new StackFrame(1).GetMethod();
            SeoRedirect(controller, request, stripQueryStrings, null, callingMethod, key);
        }

        class CachedMethod
        {
            public string Controller;
            public string Action;
            public bool IsIndex;
            public string[] PreservedParameters;
        }

        private static Regex ActionRegex = new Regex(@"\<(.*)\>");
        private static Regex ControllerRegex = new Regex(@"([^.]+)Controller");
        private static void SeoRedirect(Controller controller, HttpRequest request, QueryStringBehavior stripQueryStrings, string[] additionalPreservedKeys, MethodBase callingMethod, ulong key)
        {
            return;
            var cachedMethod = new CachedMethod
            {
                Controller = ControllerRegex.Match(callingMethod.DeclaringType.FullName).Groups[1].Value,
                IsIndex = callingMethod.Name == "Index"
            };

            cachedMethod.Action = (string)controller.RouteData.Values["action"];
            cachedMethod.Controller = (string)controller.RouteData.Values["controller"];

            if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
            {
                //Optimization: if no explicitly preserved query strings and no action parameters, avoid creating HashSet and strip all
                if ((additionalPreservedKeys == null || additionalPreservedKeys.Length == 0) && callingMethod.GetParameters().Length == 0)
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
                    bool requestHasId = request.Query.Keys.Cast<string>().Any(queryKey => queryKey == "id");
                    cachedMethod.PreservedParameters = new string[(callingMethod.GetParameters().Length + (additionalPreservedKeys?.Length ?? 0))];
                    foreach (var preserved in callingMethod.GetParameters())
                    {
                        //Optimization: remove the 'id' parameter if it's determined by the route, potentially saving on HashSet lookup entirely
                        if (!skippedId && routeHasId && !requestHasId && preserved.Name == "id")
                        {
                            //The parameter id is part of the route and not obtained via query string parameters
                            if (cachedMethod.PreservedParameters.Length == 1)
                            {
                                //Bypass everything, no need for parameter preservation
                                //This won't actually persist anywhere because it's only set once during the reflection phase and not included in the cache entry
                                //we're relying on checking if HashSet == null in DetermineSeoRedirect
#if DEBUG
                                //I'd leave it in here and rely on the compiler to strip it out, but...
                                stripQueryStrings = QueryStringBehavior.StripAll;
#endif
                                cachedMethod.PreservedParameters = null;
                                break;
                            }

                            skippedId = true;
                            var newPreserved = new string[cachedMethod.PreservedParameters.Length - 1];
                            Array.Copy(cachedMethod.PreservedParameters, 0, newPreserved, 0, i);
                            cachedMethod.PreservedParameters = newPreserved;
                            continue;
                        }
                        cachedMethod.PreservedParameters[i++] = preserved.Name;
                    }
                    if (additionalPreservedKeys != null)
                    {
                        foreach (var preserved in additionalPreservedKeys)
                        {
                            cachedMethod.PreservedParameters[i++] = preserved;
                        }
                    }

                    if (cachedMethod.PreservedParameters != null && cachedMethod.PreservedParameters.Length > 1)
                    {
                        Array.Sort(cachedMethod.PreservedParameters);
                    }
                }
            }

            MethodCache.TryAdd(key, cachedMethod);

            string destination;
            if (DetermineSeoRedirect(controller, request, cachedMethod, stripQueryStrings, out destination))
            {
                controller.Response.Headers.Add("X-Redirect-Reason", "NeoSmart SEO Rule");
                controller.Response.Redirect(destination, true);
            }
        }

        private static string MakeLegalQueryString(Controller controller, HttpRequest request, CachedMethod method, QueryStringBehavior stripQueryStrings)
        {
            if (method.PreservedParameters != null)
            {
                var sb = new StringBuilder();

                int i = 0;
                foreach (var preserved in method.PreservedParameters.Intersect(controller.Request.Query.Keys))
                {
                    sb.AppendFormat($"{(i++ == 0 ? '?' : '&')}{HttpUtility.UrlEncode(preserved)}={HttpUtility.UrlEncode(controller.Request.Query[preserved])}");
                }
                return sb.ToString();
            }

            return string.Empty;
        }

        static char[] SplitChars = new[] { '/' };
        private static (string controller, string action) ExtractRequestedRoute(string path)
        {
            var parts = path.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return (parts[0], "Index");
            }
            return (parts[0], parts[1]);
        }

        private static bool DetermineSeoRedirect(Controller controller, HttpRequest request, CachedMethod method, QueryStringBehavior stripQueryStrings, out string destination)
        {
            bool redirect = false;

            var (currentController, currentAction) = ExtractRequestedRoute(request.Path);

            //tentatively....
            //note: not using a string builder because the assumption is that most requests are correct
            destination = request.Path;

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
            //if ((method.IsIndex != destination.EndsWith("/")))
            //{
            //    // Preserve the old redirect value, i.e. only redirect if we're also redirecting for some other reason
            //    // redirect = true;
            //    destination = string.Format("{0}{1}", destination.TrimEnd('/'), method.IsIndex ? "/" : "");
            //}

            //No Index in link
            if (method.IsIndex && destination.EndsWith("/Index/"))
            {
                redirect = true;
                const string search = "Index/";
                destination = destination.Remove(destination.Length - search.Length);
            }

            //Query strings
            if (request.QueryString.HasValue)
            {
                if (stripQueryStrings == QueryStringBehavior.StripAll || method.PreservedParameters == null)
                {
                    redirect = redirect || request.Query.Count > 0;
                }
                else if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
                {
                    if (request.Query.Keys.Any(k => Array.BinarySearch(method.PreservedParameters, k) < 0))
                    {
                        redirect = true;

                        var i = 0;
                        StringBuilder qsBuilder = null;
                        foreach (var key in request.Query.Keys.Where(k => Array.BinarySearch(method.PreservedParameters, k) >= 0))
                        {
                            var value = request.Query[key];
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
                    else
                    {
                        //Keep query string parameters as-is
                        destination += request.QueryString;
                    }
                }
                else //QueryStringBehavior.KeepAll
                {
                    destination += request.QueryString;
                }
            }

            return redirect;
        }
    }
}
