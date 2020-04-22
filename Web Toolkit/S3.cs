using System;
using System.Text;
using System.Security.Cryptography;
using NeoSmart.Web.DateTimeExtensions;

namespace NeoSmart.Web
{
    public enum S3LinkType
    {
        Subdomain,
        Subfolder,
        Cname
    };

    public static class S3
    {
        private static string _username;
        private static string _password;

        public static void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public static string GetCustomExpiringLink(string cname, string objectName, TimeSpan expires)
        {
            return GetExpiringLink(cname, objectName, expires, S3LinkType.Cname);
        }

        public static string GetExpiringLink(string bucket, string objectName, TimeSpan expires, S3LinkType linkType = S3LinkType.Subdomain, bool secure = false)
        {
            var filename = System.IO.Path.GetFileName(objectName);
            filename = filename.Replace("%20", " ");
            filename = filename.Replace("+", " ");

            objectName = objectName.Replace("%20", " ");
            objectName = Uri.EscapeUriString(objectName);
            objectName = objectName.Replace("%2F", "/");
            objectName = objectName.Replace("+", "%20");

            string s3Url;
            if (linkType == S3LinkType.Subfolder)
            {
                s3Url = $"http{(secure ? "s" : "")}://s3.amazonaws.com/{bucket}";
            }
            else
            {
                s3Url = $"http{(secure ? "s" : "")}://{bucket}{(linkType == S3LinkType.Cname ? "" : ".s3.amazonaws.com")}";
            }

            long expiresTime = ((long)expires.TotalSeconds) + (long)DateTime.UtcNow.ToUnixTimeSeconds();
            string bucketName = "/" + bucket;
            string options = string.Format("response-content-disposition=attachment; filename=\"{0}\"", filename);

            string toSign = string.Format("GET\n\n\n{0}\n{1}{2}?{3}", expiresTime, bucketName, objectName, options);

            using (var sha1 = new HMACSHA1(Encoding.UTF8.GetBytes(_password)))
            {
                var signed = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
                var encoded = Uri.EscapeDataString(Convert.ToBase64String(signed));

                options = options.Replace(" ", "%20");

                return $"{s3Url}{string.Empty}{objectName}?AWSAccessKeyId={_username}&Expires={expiresTime}&Signature={encoded}&{options}";
            }
        }
    }
}
