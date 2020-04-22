using System;
using System.Text.Json;

namespace NeoSmart.Web
{
    public static class JsonExtensions
    {
        private static JsonSerializerOptions Normal = new JsonSerializerOptions();
        private static JsonSerializerOptions Indented = new JsonSerializerOptions() { WriteIndented = true };

        public static string ToJson<T>(this T t, JsonSerializerOptions options)
        {
            return JsonSerializer.Serialize(t, options);
        }

        public static string ToJson<T>(this T t, bool indented = true)
        {
            return t.ToJson(indented ? Indented : Normal);
        }

        public static bool IsJson(this string text)
        {
            return IsJson(text.AsSpan());
        }

        public static bool IsJson(this ReadOnlySpan<char> text)
        {
            var trimmed = text.Trim();
            if ((!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) && (!trimmed.StartsWith("[") || !trimmed.EndsWith("]")))
            {
                return false;
            }

            return true;
        }
    }
}
