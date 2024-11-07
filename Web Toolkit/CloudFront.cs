using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeoSmart.Utils;

namespace NeoSmart.Web
{

    internal static class RSAKeyExtensions
        {
            public static void FromXmlString(this RSA rsa, string xmlString)
            {
                RSAParameters parameters = new RSAParameters();

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlString);

                if (xmlDoc.DocumentElement!.Name.Equals("RSAKeyValue"))
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

    internal class TrueCamelCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return $"{char.ToUpper(name[0])}{name.Substring(1)}";
        }
    }

    public static class CloudFront
    {
        private static string _rsaXml = null!;
        private static string _keyPairId = null!;

        private static readonly JsonSerializerOptions CamelCasedJsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = new TrueCamelCaseNamingPolicy(),
        };

        public static void SetCredentials(string keyPairId, string rsaXml)
        {
            _rsaXml = rsaXml;
            _keyPairId = keyPairId;
        }

        private static byte[] GetSignature(byte[] toSign)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = sha1.ComputeHash(toSign);
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
            objectName = Uri.EscapeDataString(objectName);
            objectName = objectName.Replace("%2F", "/");
            objectName = objectName.Replace("+", "%20");

            long maxAgeSeconds = maxAge == null ? 1209600 : (long) maxAge.Value.TotalSeconds;

            string contentDisposition = Uri.EscapeDataString(string.Format("attachment; filename=\"{0}\"", filename)).Replace(" ", "%20");
            string cacheControl = Uri.EscapeDataString(string.Format("max-age={0}", maxAgeSeconds)).Replace(" ", "%20");
            string baseUrl = $"http{(secure ? "s" : "")}://{domainName}{objectName}?response-content-disposition={contentDisposition}&response-cache-control={cacheControl}";

            var policy = new
            {
                Statement = new[]
                {
                    new
                    {
                        Resource = baseUrl,
                        Condition = new
                        {
                            DateLessThan = new AwsEpochTime(expiresTime),
                        }
                    }
                }
            };

            var policyBytes = JsonSerializer.SerializeToUtf8Bytes(policy, CamelCasedJsonOptions);
            // CloudFront does not use the same format as UrlBase64
            //var encodedPolicy = UrlBase64.Encode(policyBytes);
            //var encodedSignature = UrlBase64.Encode(GetSignature(policyBytes));
            var encodedPolicy = ToUrlSafeBase64String(policyBytes);
            var encodedSignature = ToUrlSafeBase64String(GetSignature(policyBytes));

            return $"{baseUrl}&Policy={encodedPolicy}&Signature={encodedSignature}&Key-Pair-Id={_keyPairId}";
        }

        readonly struct AwsEpochTime
        {
            [JsonPropertyName("AWS:EpochTime")]
            public long EpochTime { get; }

            public AwsEpochTime(DateTimeOffset epochTime)
            {
                EpochTime = epochTime.ToUnixTimeSeconds();
            }
        }
    }
}
