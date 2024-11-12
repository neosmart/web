using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using HttpUtility = System.Web.HttpUtility;

namespace NeoSmart.Web
{
    static public class Seo
    {
        public static ILogger? Logger { private get; set; } = null!;

        public static List<string> PreservedQueryStrings = new()
        {
            "gad",
            "gad_source",
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

        public static void RobotsTag(this Controller controller, string value, string? botName = null)
        {
            //if (controller.Response.Headers.ContainsKey("X-Robots-Tag"))
            //{
            //    var old = controller.Response.Headers["X-Robots-Key"].ToString();
            //    controller.Response.Headers.Remove("X-Robots-Key");
            //    value = $"{old}, {value}";
            //}
            controller.Response.Headers.Append("X-Robots-Tag", string.Format("{0}{1}{2}", botName ?? string.Empty, botName != null ? ": " : string.Empty, value));
        }

        public static void NoIndex(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "noindex", botName);
        }

        public static void NoFollow(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "nofollow", botName);
        }

        public static void NoIndexNoFollow(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "noindex, nofollow", botName);
        }

        public static void NoArchive(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "noarchive", botName);
        }

        public static void NoSnippet(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "nosnippet", botName);
        }

        public static void NoTranslate(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "notranslate", botName);
        }

        public static void NoImageIndex(this Controller controller, string? botName = null)
        {
            RobotsTag(controller, "noimageindex", botName);
        }

        public static void UnavailableAfter(this Controller controller, DateTime max, string? botName = null)
        {
            //Google can't make up its mind. First it asks for RFC 850 then in an example it uses something else!
            //https://developers.google.com/webmasters/control-crawl-index/docs/robots_meta_tag
            //The globally-accepted "web standard" http format is RFC1123. https://www.hackcraft.net/web/datetime/#rfc850
            RobotsTag(controller, $"unavailable_after: {max:R}", botName);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="request"></param>
        /// <param name="extraQueryStrings">The query strings to preserve, in addition to the route's explicit GET parameters and <see cref="PreservedQueryStrings"/></param>
        /// <param name="filePath"></param>
        /// <param name="lineNumber"></param>
        public static void SeoRedirect(this Controller controller, HttpRequest request, string[]? extraQueryStrings = null, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            List<string>? preservedQueryStrings;
            if (extraQueryStrings is null)
            {
                // Pass through the default state, skipping allocations and also causing it to update dynamically.
                preservedQueryStrings = PreservedQueryStrings;
            }
            else
            {
                preservedQueryStrings = new(PreservedQueryStrings);
                preservedQueryStrings.AddRange(extraQueryStrings);
                preservedQueryStrings.Sort(StringComparer.Ordinal);
            }

            var key = NeoSmart.Hashing.XXHash.XXHash64.Hash((ulong)lineNumber, MemoryMarshal.AsBytes(filePath.AsSpan()));
            if (MethodCache.TryGetValue(key, out var cachedMethod))
            {
                string? destination;
                if (DetermineSeoRedirect(controller, request, cachedMethod, QueryStringBehavior.KeepActionParameters, out destination))
                {
                    controller.Response.Redirect(destination, true);
                }
                return;
            }

            // There has to be a caller because this isn't Main()
            var callingMethod = new StackFrame(1).GetMethod()!;
            SeoRedirect(controller, request, QueryStringBehavior.KeepActionParameters, preservedQueryStrings, callingMethod, key);
        }

        public static void SeoRedirect(this Controller controller, HttpRequest request, QueryStringBehavior stripQueryStrings, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
        {
            var key = Hashing.XXHash.XXHash64.Hash((ulong)lineNumber, MemoryMarshal.AsBytes(filePath.AsSpan()));
            if (MethodCache.TryGetValue(key, out var cachedMethod))
            {
                string? destination;
                if (DetermineSeoRedirect(controller, request, cachedMethod, stripQueryStrings, out destination))
                {
                    controller.Response.Redirect(destination, true);
                }
                return;
            }

            var callingMethod = new StackFrame(1).GetMethod()!;
            SeoRedirect(controller, request, stripQueryStrings, null, callingMethod, key);
        }

        class CachedMethod
        {
            public string Controller { get; }
            public string Action { get; }
            public bool IsIndex { get; set; }
            public List<string>? PreservedParameters { get; set; }

            public CachedMethod(string controller, string action)
            {
                Controller = controller;
                Action = action;
            }
        }

        private static HashSet<Type> IgnoredParameterAttrs = new()
        {
            typeof(FromBodyAttribute),
            typeof(FromFormAttribute),
            typeof(FromHeaderAttribute),
            typeof(FromRouteAttribute),
            typeof(FromServicesAttribute),
        };

        private static Regex ActionRegex = new Regex(@"\<(.*)\>");
        private static Regex ControllerRegex = new Regex(@"([^.]+)Controller");

        /// <summary>
        ///
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="request"></param>
        /// <param name="stripQueryStrings"></param>
        /// <param name="preservedKeys">The keys to preserve in addition to the action's GET parameters if <paramref name="stripQueryStrings"/>
        /// is <see cref="QueryStringBehavior.KeepActionParameters"/>.</param>
        /// <param name="callingMethod"></param>
        /// <param name="key"></param>
        private static void SeoRedirect(Controller controller, HttpRequest request, QueryStringBehavior stripQueryStrings, List<string>? preservedKeys, MethodBase callingMethod, ulong key)
        {
            var cachedMethod = new CachedMethod
            (
                // ControllerRegex.Match(callingMethod.DeclaringType.FullName).Groups[1].Value,
                (string)controller.RouteData.Values["controller"],
                (string)controller.RouteData.Values["action"]
            )
            {
                IsIndex = callingMethod.Name == "Index"
            };

            if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
            {
                // Optimization: if no explicitly preserved query strings and no action parameters, avoid creating HashSet and strip all
                if ((preservedKeys is null || preservedKeys.Count == 0) && callingMethod.GetParameters().Length == 0)
                {
                    // This won't actually persist anywhere because it's only set once during the reflection phase and not included in the cache entry.
                    // We're relying on checking if (HashSet is null) in DetermineSeoRedirect()
                }
                else
                {
                    // bool routeHasId = controller.RouteData.Values.ContainsKey("id");
                    // bool requestHasId = request.Query.Keys.Cast<string>().Any(queryKey => queryKey.Equals("id", StringComparison.Ordinal));
                    var methodParams = callingMethod.GetParameters();
                    cachedMethod.PreservedParameters = null;
                    foreach (var methodParam in methodParams)
                    {
                        if (methodParam.CustomAttributes.Any(attr => IgnoredParameterAttrs.Contains(attr.AttributeType)))
                        {
                            continue;
                        }

                        cachedMethod.PreservedParameters ??= preservedKeys is not null ? new(preservedKeys) : new();
                        cachedMethod.PreservedParameters.Add(methodParam.Name!);
                    }
                    if (cachedMethod.PreservedParameters is not null)
                    {
                        cachedMethod.PreservedParameters.Sort(StringComparer.Ordinal);
                    }
                    else
                    {
                        // Reuse the existing object and avoid extra allocations
                        cachedMethod.PreservedParameters = preservedKeys;
                        // cachedMethod.PreservedParameters might still be null!
                    }
                }
            }

            MethodCache.TryAdd(key, cachedMethod);

            if (DetermineSeoRedirect(controller, request, cachedMethod, stripQueryStrings, out var destination))
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
        private static (string controller, string action) ExtractControllerAndAction(string path)
        {
            var parts = path.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return ("", "Index");
            }
            else if (parts.Length == 1)
            {
                return (parts[0], "Index");
            }
            return (parts[0], parts[1]);
        }

        private static bool DetermineSeoRedirect(Controller controller, HttpRequest request, CachedMethod method, QueryStringBehavior stripQueryStrings, [NotNullWhen(true)] out string? destination)
        {
            var (currentController, currentAction) = ExtractControllerAndAction(request.Path);

            // Tentatively....
            // Note: not using a string builder because the assumption is that most requests are correct
            destination = null;
            static string InitDestination(HttpRequest request)
            {
                return request.PathBase == "" ? request.Path : $"{request.PathBase}{request.Path}";
            }

            // Case-based redirect
            if (!currentAction.Equals(method.Action, StringComparison.Ordinal))
            {
                destination ??= InitDestination(request);
                destination = destination.Replace(currentAction, method.Action, StringComparison.Ordinal);
            }

            // Case-based redirect
            if (!string.IsNullOrEmpty(currentController) && !currentController.Equals(method.Controller, StringComparison.Ordinal))
            {
                destination ??= InitDestination(request);
                destination = destination.Replace(currentController, method.Controller, StringComparison.Ordinal);
            }

            // Trailing-backslash configuration (this is the simplification of the NOT XNOR)
            //if ((method.IsIndex != destination.EndsWith("/")))
            //{
            //    // Preserve the old redirect value, i.e. only redirect if we're also redirecting for some other reason
            //    // redirect = true;
            //    destination = string.Format("{0}{1}", destination.TrimEnd('/'), method.IsIndex ? "/" : "");
            //}

            // No Index in link
            if (method.IsIndex)
            {
                if ((destination ?? request.Path).EndsWith("/Index/", StringComparison.Ordinal))
                {
                    destination ??= InitDestination(request);
                    destination = destination.Remove(destination.Length - "Index/".Length);

                }
                else if ((destination ?? request.Path).EndsWith("/Index", StringComparison.Ordinal))
                {
                    destination ??= InitDestination(request);
                    destination = destination.Remove(destination.Length - "Index".Length);
                }
            }

            // Query strings
            if (request.QueryString.HasValue)
            {
                if (stripQueryStrings == QueryStringBehavior.StripAll || method.PreservedParameters is null)
                {
                    if (request.Query.Count > 0)
                    {
                        destination ??= InitDestination(request);
                    }
                }
                else if (stripQueryStrings == QueryStringBehavior.KeepActionParameters)
                {
                    if (request.Query.Keys.Any(k => method.PreservedParameters.BinarySearch(k, StringComparer.Ordinal) < 0))
                    {
                        var i = 0;
                        StringBuilder? qsBuilder = null;
                        foreach (var key in request.Query.Keys.Where(k => method.PreservedParameters.BinarySearch(k, StringComparer.Ordinal) >= 0))
                        {
                            var value = request.Query[key];
                            qsBuilder ??= new StringBuilder();
                            qsBuilder.AppendFormat("{0}{1}{2}{3}", i == 0 ? '?' : '&', key,
                                string.IsNullOrEmpty(value) ? "" : "=",
                                string.IsNullOrEmpty(value) ? "" : HttpUtility.UrlEncode(value));
                            ++i;
                        }

                        if (qsBuilder is not null)
                        {
                            destination ??= InitDestination(request);
                            destination += qsBuilder.ToString();
                        }
                    }
                    else if (destination is not null)
                    {
                        // Keep query string parameters as-is
                        destination += request.QueryString;
                    }
                }
                else if (destination is not null) // QueryStringBehavior.KeepAll
                {
                    destination += request.QueryString;
                }
            }

            if (destination is not null)
            {
                Logger?.LogDebug("Redirecting {OriginalUri} to {RedirectUri}",
                    request.GetEncodedPathAndQuery(), destination);
                return true;
            }

            return false;
        }
    }
}
