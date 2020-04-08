using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace NeoSmart.Web
{
    using System;
    using System.Security.Cryptography;
    using System.Xml;
    using Newtonsoft.Json;

        internal static class RSAKeyExtensions
        {
            public static void FromXmlString(this RSA rsa, string xmlString)
            {
                RSAParameters parameters = new RSAParameters();

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlString);

                if (xmlDoc.DocumentElement.Name.Equals("RSAKeyValue"))
                {
                    foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                    {
                        switch (node.Name)
                        {
                            case "Modulus": parameters.Modulus = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "Exponent": parameters.Exponent = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "P": parameters.P = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "Q": parameters.Q = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "DP": parameters.DP = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "DQ": parameters.DQ = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "InverseQ": parameters.InverseQ = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                            case "D": parameters.D = (string.IsNullOrEmpty(node.InnerText) ? null : Convert.FromBase64String(node.InnerText)); break;
                        }
                    }
                }
                else
                {
                    throw new Exception("Invalid XML RSA key.");
                }

                rsa.ImportParameters(parameters);
            }

            public static string ToXmlString(this RSA rsa, bool includePrivateParameters)
            {
                RSAParameters parameters = rsa.ExportParameters(includePrivateParameters);

                return string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent><P>{2}</P><Q>{3}</Q><DP>{4}</DP><DQ>{5}</DQ><InverseQ>{6}</InverseQ><D>{7}</D></RSAKeyValue>",
                      parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
                      parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null,
                      parameters.P != null ? Convert.ToBase64String(parameters.P) : null,
                      parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null,
                      parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null,
                      parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null,
                      parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null,
                      parameters.D != null ? Convert.ToBase64String(parameters.D) : null);
            }
        }

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
                    RSAKeyExtensions.FromXmlString(rsa, _rsaXml);
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
