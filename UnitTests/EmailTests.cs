using System.Collections.Generic;
using System.Net.Mail;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSmart.Web;

namespace UnitTests
{
    [TestClass]
    public class EmailTests
    {
        /// <summary>
        /// Check variations of common email address domains and verify
        /// that they are correctly caught as likely typos.
        /// </summary>
        [TestMethod]
        public void SimilarityCheckExpectedFail()
        {
            Dictionary<string, string[]> tests = new()
            {
                {
                    "gmail.com",
                    new[]
                    {
                    "gaiml.com",
                    "gmail.cmo",
                    //"gamil.co",
                    "gmail.cm",
                    "gmial.com",
                    "gmail.co",
                }
                },
                {
                    "hotmail.com",
                    new[]
                {
                    "hitmail.com",
                    "hotmail.co",
                }
                },
            };

            foreach (var (actualDomain, typos) in tests)
            {
                foreach (var typoDomain in typos)
                {
                    var mailAddress = new MailAddress($"foo@{typoDomain}");
                    Assert.IsTrue(
                        EmailFilter.HasMaybeMistypedDomain(mailAddress, out var corrected, out var similarity),
                        $"Domain {typoDomain} not registered as a typo of {actualDomain} (reported similarity: {similarity}");

                    Assert.AreEqual(actualDomain, corrected!.Host, $"Domain detected as a typo of {corrected.Host} and not {actualDomain}");
                }
            }
        }

        [TestMethod]
        public void TestHasValidMx()
        {
            Assert.IsTrue(EmailFilter.HasValidMx("foo@outlook.com"));
        }
    }
}