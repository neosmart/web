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

        public CdnRewriteFilter(Stream stream)
        {
            Stream = stream;
        }

        public RewriteObjects RewriteType { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var html = Encoding.Default.GetString(buffer, offset, count);

            if ((RewriteType & RewriteObjects.Images) == RewriteObjects.Images)
            {
                html = Regex.Replace(html, "<[^>]*img[^>]+src=\"(/[^/][^\"]+)\"", PrefixDomain, RegexOptions.IgnoreCase);
                html = Regex.Replace(html, "<[^>]*img[^>]+src='(/[^/][^']+)'", PrefixDomain, RegexOptions.IgnoreCase);
            }

            if ((RewriteType & RewriteObjects.JavaScript) == RewriteObjects.JavaScript)
            {
                html = Regex.Replace(html, "\"(/[^/][^\"]+.js)\"", PrefixDomain, RegexOptions.IgnoreCase);
                html = Regex.Replace(html, "'(/[^/][^\"]+.js)'", PrefixDomain, RegexOptions.IgnoreCase);
            }

            if ((RewriteType & RewriteObjects.Css) == RewriteObjects.Css)
            {
                html = Regex.Replace(html, "\"(/[^/][^\"]+.css)\"", PrefixDomain, RegexOptions.IgnoreCase);
                html = Regex.Replace(html, "'(/[^/][^\"]+.css)'", PrefixDomain, RegexOptions.IgnoreCase);
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
