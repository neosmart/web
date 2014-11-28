using System;
using System.Text;
using System.Security.Cryptography;
using Amazon.Util;

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
            string filename = System.IO.Path.GetFileName(objectName);
            filename = filename.Replace("%20", " ");
            filename = filename.Replace("+", " ");

            objectName = objectName.Replace("%20", " ");
            objectName = Uri.EscapeUriString(objectName);
            objectName = objectName.Replace("%2F", "/");
            objectName = objectName.Replace("+", "%20");

            string s3Url;
            if (linkType == S3LinkType.Subfolder)
            {
                s3Url = string.Format("http{0}://s3.amazonaws.com/{1}", secure ? "s" : "", bucket);
            }
            else
            {
                s3Url = string.Format("http{0}://{1}{2}", secure ? "s" : "", bucket, linkType == S3LinkType.Cname ? "" : ".s3.amazonaws.com");
            }

            Int64 expiresTime = ((Int64)expires.TotalSeconds) + (Int64)AWSSDKUtils.ConvertToUnixEpochMilliSeconds(DateTime.UtcNow);
            string bucketName = "/" + bucket;
            string options = string.Format("response-content-disposition=attachment; filename=\"{0}\"", filename);

            string toSign = string.Format("GET\n\n\n{0}\n{1}{2}?{3}", expiresTime, bucketName, objectName, options);

            var signed = new HMACSHA1(Encoding.UTF8.GetBytes(_password)).ComputeHash(Encoding.UTF8.GetBytes(toSign));
            string encoded = Uri.EscapeDataString(Convert.ToBase64String(signed));

            options = options.Replace(" ", "%20");
            
            string url = string.Format("{0}{1}{2}?AWSAccessKeyId={3}&Expires={4}&Signature={5}&{6}",
                s3Url, string.Empty, objectName, _username, expiresTime, encoded, options);

            return url;
        }
    }
}
