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
        public Stream Stream { get; set; }
        public string CdnDomain { get; set; }

        private readonly Regex[] _jsRegexes;
        private readonly Regex[] _imgRegexes;
        private readonly Regex[] _cssRegexes;

        public CdnRewriteFilter(Stream stream)
        {
            Stream = stream;

            _jsRegexes = new[]
                {
                    new Regex("\"(/[^/][^\"]+.js)\"", RegexOptions.IgnoreCase),
                    new Regex("'(/[^/][^\"]+.js)'", RegexOptions.IgnoreCase)
                };

            _cssRegexes = new[]
                {
                    new Regex("\"(/[^/][^\"]+.css)\"", RegexOptions.IgnoreCase),
                    new Regex("'(/[^/][^\"]+.css)'", RegexOptions.IgnoreCase)
                };

            _imgRegexes = new[]
                {
                    new Regex("<[^>]*img[^>]+src=\"(/[^/][^\"]+)\"", RegexOptions.IgnoreCase),
                    new Regex("<[^>]*img[^>]+src='(/[^/][^']+)'", RegexOptions.IgnoreCase)
                };
        }

        public RewriteObjects RewriteType { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var html = Encoding.Default.GetString(buffer, offset, count);

            if ((RewriteType & RewriteObjects.Images) == RewriteObjects.Images)
            {
                html = _imgRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }

            if ((RewriteType & RewriteObjects.JavaScript) == RewriteObjects.JavaScript)
            {
                html = _jsRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }

            if ((RewriteType & RewriteObjects.Css) == RewriteObjects.Css)
            {
                html = _cssRegexes.Aggregate(html, (current, regex) => regex.Replace(current, PrefixDomain));
            }
            
            byte[] outData = Encoding.Default.GetBytes(html);
            Stream.Write(outData, 0, outData.GetLength(0));
        }

        private string PrefixDomain(Match match)
        {
            return match.Value.Replace(match.Groups[1].Value, string.Format("//{0}{1}", CdnDomain, match.Groups[1].Value));
        }
    }
}
