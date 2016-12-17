using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using NeoSmart.ExtensionMethods;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace NeoSmart.Web
{
    public static class CloudFront
    {
        private static string _rsaXml;
        private static string _keyPairId;

        public static void SetCredentials(string keyPairId, string rsaXml)
        {
            _rsaXml = rsaXml;
            _keyPairId = keyPairId;
        }


        private static byte[] GetSignature(string toSign)
        {
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var bytes = sha1.ComputeHash(Encoding.ASCII.GetBytes(toSign));
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(_rsaXml);
                    var formatter = new RSAPKCS1SignatureFormatter(rsa);
                    formatter.SetHashAlgorithm("SHA1");
                    return formatter.CreateSignature(bytes);
                }
            }
        }

        private static string ToUrlSafeBase64String(byte[] input)
        {
            return Convert.ToBase64String(input)
                    .Replace('+', '-')
                    .Replace('=', '_')
                    .Replace('/', '~');
        }

        public static string GetExpiringLink(string domainName, string objectName, TimeSpan expires, TimeSpan? maxAge = null, bool secure = false)
        {
            return GetExpiringLink(domainName, objectName, DateTime.UtcNow + expires, maxAge, secure);
        }

        public static string GetExpiringLink(string domainName, string objectName, DateTime expiresTime, TimeSpan? maxAge = null, bool secure = false)
        {
            string filename = Path.GetFileName(objectName);
            filename = filename.Replace("%20", " ");
            filename = filename.Replace("+", " ");

            objectName = objectName.Replace("%20", " ");
            objectName = Uri.EscapeUriString(objectName);
            objectName = objectName.Replace("%2F", "/");
            objectName = objectName.Replace("+", "%20");

            Int64 maxAgeSeconds = maxAge == null ? 1209600 : (Int64) maxAge.Value.TotalSeconds;

            var expiresEpoch = (Int64) expiresTime.ToUnixTimeSeconds();
            string contentDisposition = Uri.EscapeDataString(string.Format("attachment; filename=\"{0}\"", filename)).Replace(" ", "%20");
            string cacheControl = Uri.EscapeDataString(string.Format("max-age={0}", maxAgeSeconds)).Replace(" ", "%20");
            string baseUrl = string.Format("http{0}://{1}{2}?response-content-disposition={3}&response-cache-control={4}", secure ? "s" : "", domainName, objectName, contentDisposition, cacheControl);

            var policy = new
            {
                Statement = new[]
                {
                    new
                    {
                        Resource = baseUrl,
                        Condition = new
                        {
                            DateLessThan = new
                            {
                                AWS_EpochTime = expiresEpoch
                            }
                        }
                    }
                }
            };

            var policyString = JsonConvert.SerializeObject(policy).Replace("AWS_", "AWS:");
            var encodedPolicy = ToUrlSafeBase64String(Encoding.ASCII.GetBytes(policyString));
            var encodedSignature = ToUrlSafeBase64String(GetSignature(policyString));

            return $"{baseUrl}&Policy={encodedPolicy}&Signature={encodedSignature}&Key-Pair-Id={_keyPairId}";
        }
    }
}
