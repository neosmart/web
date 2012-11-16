using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    [Flags]
    public enum RewriteObjects
    {
        None = 0,
        Images = 0x01,
        Css = 0x02,
        JavaScript = 0x04,
        All = 0xFF
    }

    public class CdnRewriteFilter : MemoryStream
    {
        private readonly Stream _stream;
        public string CdnDomain { get; set; }

        private static readonly Regex[] JsRegexes;
        private static readonly Regex[] ImgRegexes;
        private static readonly Regex[] CssRegexes;

        static CdnRewriteFilter()
        {
            JsRegexes = new[]
                {
                    new Regex("\"(/[^/][^\"]+.js)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                    new Regex("'(/[^/][^\"]+.js)'", RegexOptions.IgnoreCase | RegexOptions.Compiled)
                };

            CssRegexes = new[]
                {
                    new Regex("\"(/[^/][^\"]+.css)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                    new Regex("'(/[^/][^\"]+.css)'", RegexOptions.IgnoreCase | RegexOptions.Compiled)
                };

            ImgRegexes = new[]
                {
                    new Regex("<[^>]*img[^>]+src=\"(/[^/][^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                    new Regex("<[^>]*img[^>]+src='(/[^/][^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled)
                };
        }

        public CdnRewriteFilter(Stream stream)
        {
            _stream = stream;
        }

        public RewriteObjects RewriteType { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var html = Encoding.Default.GetString(buffer, offset, count);

            if ((RewriteType & RewriteObjects.Images) == RewriteObjects.Images)
            {
                html = ImgRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }

            if ((RewriteType & RewriteObjects.JavaScript) == RewriteObjects.JavaScript)
            {
                html = JsRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }

            if ((RewriteType & RewriteObjects.Css) == RewriteObjects.Css)
            {
                html = CssRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }
            
            byte[] outData = Encoding.Default.GetBytes(html);
            _stream.Write(outData, 0, outData.GetLength(0));
        }

        private string PrefixDomain(Match match)
        {
            return match.Value.Replace(match.Groups[1].Value, string.Format("//{0}{1}", CdnDomain, match.Groups[1].Value));
        }
    }
}
