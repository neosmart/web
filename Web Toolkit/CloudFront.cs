using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using Amazon.Util;
using NServiceKit.Text;

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

        public static string GetExpiringLink(string domainName, string objectName, TimeSpan expires, bool secure = false)
        {
            string filename = Path.GetFileName(objectName);
            filename = filename.Replace("%20", " ");
            filename = filename.Replace("+", " ");

            objectName = objectName.Replace("%20", " ");
            objectName = Uri.EscapeUriString(objectName);
            objectName = objectName.Replace("%2F", "/");
            objectName = objectName.Replace("+", "%20");

            var expiresTime = DateTime.UtcNow + expires;
            var expiresEpoch = AWSSDKUtils.ConvertToUnixEpochSeconds(expiresTime);
            string options = Uri.EscapeDataString(string.Format("attachment; filename=\"{0}\"", filename)).Replace(" ", "%20");
            string baseUrl = string.Format("http{0}://{1}{2}?response-content-disposition={3}", secure ? "s" : "", domainName, objectName, options);

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

            string policyString = policy.ToJson().Replace("AWS_", "AWS:");
            string encodedPolicy = ToUrlSafeBase64String(Encoding.ASCII.GetBytes(policyString));
            string encodedSignature = ToUrlSafeBase64String(GetSignature(policyString));

            string url = string.Format("{0}&Policy={1}&Signature={2}&Key-Pair-Id={3}", baseUrl, encodedPolicy, encodedSignature, _keyPairId);

            return url;
        }
    }
}
