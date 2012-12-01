using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NeoSmart.Web
{
    static public class RegexUtilities
    {
        private static readonly Regex EmailRegex = new Regex(@"^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                                     @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$",
                                     RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));

        static public bool IsValidEmail(string email)
        {
            bool invalid = false;
            if (String.IsNullOrEmpty(email))
            {
                return false;
            }
            
            try
            {
                email = Regex.Replace(email, @"(@)(.+)$", match =>
                    {
                        //use IdnMapping class to convert Unicode domain names. 
                        var idn = new IdnMapping();

                        string domainName = match.Groups[2].Value;
                        try
                        {
                            domainName = idn.GetAscii(domainName);
                        }
                        catch (ArgumentException)
                        {
                            invalid = true;
                        }

                        return match.Groups[1].Value + domainName;
                    },
                    RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }

            if (invalid)
            {
                return false;
            }

            //return true if input is in valid e-mail format. 
            try
            {
                return EmailRegex.IsMatch(email);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
