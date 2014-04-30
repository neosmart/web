using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public class EmailFilter
    {
        private static readonly Regex EmailRegex = new Regex(@"^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                                     @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$",
                                     RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
        private static readonly Regex NumericEmailRegex = new Regex(@"^[0-9]+@", RegexOptions.Compiled);
        private static readonly Regex NumericDomainRegex = new Regex(@"@[0-9]+\.[^.]+$", RegexOptions.Compiled);
        private static readonly Regex TldRegex = new Regex(@"\.(ru|cn|info|tk|om)$", RegexOptions.Compiled);
        private static readonly Regex DomainPrefixRegex = new Regex(@"@(spam|webmaster|example|nospam|junk|none|test|sample)", RegexOptions.Compiled);
        private static readonly Regex PrefixRegex = new Regex(@"^(postmaster|abuse|nospam|spam|junk|noone|none|no1|test|example|sample|thanks|nothank|noway)", RegexOptions.Compiled);
        private static readonly Regex WordFilterRegex = new Regex(@"fuck|bitch|bastard|spam|junk", RegexOptions.Compiled);
        private static readonly Regex QwertyRegex = new Regex(@"^[asdfghjkl]+@", RegexOptions.Compiled);

        static private readonly HashSet<string> ValidDomainCache = new HashSet<string>();

        static public bool IsProbablyFakeEmail(string email, int meanness, bool validateMx = false)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return true;
            }

            //Instead of making all the regex rules case-insensitive
            email = email.ToLower();

            if (meanness >= 0)
            {
                if (!IsValidFormat(email))
                    return true;
            }

            var mailAddress = new MailAddress(email);

            if (meanness >= 1)
            {
                if (BlockedDomains.Contains(mailAddress.Host))
                    return true;
            }
            if (meanness >= 2)
            {
                if (PrefixRegex.IsMatch(email))
                    return true;
                if (DomainPrefixRegex.IsMatch(email))
                    return true;
            }
            if (meanness >= 5)
            {
                if (NumericEmailRegex.IsMatch(email))
                    return true;
                if (QwertyRegex.IsMatch(email))
                    return true;
            }
            if (meanness >= 6)
            {
                if (WordFilterRegex.IsMatch(email))
                    return true;
                if (NumericDomainRegex.IsMatch(email))
                    return true;
            }
            if (meanness >= 10)
            {
                if (TldRegex.IsMatch(email))
                    return true;
            }

            //Do this last because it's the most expensive
            if (validateMx)
            {
                if (!ValidDomainCache.Contains(mailAddress.Host))
                {
                    bool mxFound;
                    DnsLookup.GetMXRecords(mailAddress.Host, out mxFound);
                    if (!mxFound)
                    {
                        return true;
                    }
                    ValidDomainCache.Add(mailAddress.Host);
                }
            }

            return false;
        }

        //From http://msdn.microsoft.com/en-us/library/01escwtf.aspx
        static public bool IsValidFormat(string email)
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
                }, RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
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

        //Originally from http://www.digitalfaq.com/forum/web-tech/5050-throwaway-email-block.html
        private readonly static HashSet<string> BlockedDomains = new HashSet<string>(new[]
            {
                "0clickemail.com",
                "10minutemail.com",
                "10minutemail.de",
                "123-m.com",
                "126.com",
                "139.com",
                "163.com",
                "1pad.de",
                "20minutemail.com",
                "21cn.com",
                "2prong.com",
                "33mail.com",
                "3d-painting.com",
                "4warding.com",
                "4warding.net",
                "4warding.org",
                "6paq.com",
                "60minutemail.com",
                "7days-printing.com",
                "7tags.com",
                "99experts.com",
                "agedmail.com",
                "amilegit.com",
                "ano-mail.net",
                "anonbox.net",
                "anonymbox.com",
                "antispam.de",
                "armyspy.com",
                "beefmilk.com",
                "bigstring.com",
                "binkmail.com",
                "bio-muesli.net",
                "bobmail.info",
                "bofthew.com",
                "boxformail.in",
                "brefmail.com",
                "brennendesreich.de",
                "broadbandninja.com",
                "bsnow.net",
                "buffemail.com",
                "bugmenot.com",
                "bumpymail.com",
                "bund.us",
                "cellurl.com",
                "chammy.info",
                "cheatmail.de",
                "chogmail.com",
                "chong-mail.com",
                "chong-mail.net",
                "chong-mail.org",
                "clixser.com",
                "cmail.com",
                "cmail.net",
                "cmail.org",
                "consumerriot.com",
                "cool.fr.nf",
                "courriel.fr.nf",
                "courrieltemporaire.com",
                "c2.hu",
                "curryworld.de",
                "cust.in",
                "cuvox.de",
                "dacoolest.com",
                "dandikmail.com",
                "dayrep.com",
                "dbunker.com",
                "dcemail.com",
                "deadaddress.com",
                "deagot.com",
                "dealja.com",
                "despam.it",
                "devnullmail.com",
                "digitalsanctuary.com",
                "dingbone.com",
                "discardmail.com",
                "discardmail.de",
                "dispose.it",
                "disposableinbox.com",
                "disposeamail.com",
                "dispostable.com",
                "dodgeit.com",
                "dodgit.com",
                "dodgit.org",
                "domozmail.com",
                "dontreg.com",
                "dontsendmespam.de",
                "drdrb.com",
                "drdrb.net",
                "dudmail.com",
                "dump-email.info",
                "dumpyemail.com",
                "duskmail.com",
                "e-mail.com",
                "e-mail.org",
                "e4ward.com",
                "easytrashmail.com",
                "einrot.de",
                "email.com",
                "emailgo.de",
                "emailias.com",
                "email60.com",
                "emailinfive.com",
                "emaillime.com",
                "emailmiser.com",
                "emailtemporario.com.br",
                "emailtemporar.ro",
                "emailthe.net",
                "emailtmp.com",
                "emailwarden.com",
                "example.com",
                "example.net",
                "example.org",
                "explodemail.com",
                "fakeinbox.com",
                "fakeinformation.com",
                "fakemail.fr",
                "fantasymail.de",
                "fastacura.com",
                "fatflap.com",
                "fdfdsfds.com",
                "fightallspam.com",
                "filzmail.com",
                "fizmail.com",
                "flyspam.com",
                "fr33mail.info",
                "frapmail.com",
                "friendlymail.co.uk",
                "fuckingduh.com",
                "fudgerub.com",
                "garliclife.com",
                "get1mail.com",
                "get2mail.fr",
                "getairmail.com",
                "getmails.eu",
                "getonemail.com",
                "getonemail.net",
                "gishpuppy.com",
                "gmai.com",
                "goemailgo.com",
                "gotmail.com",
                "gotmail.net",
                "gotmail.org",
                "gotti.otherinbox.com",
                "great-host.in",
                "guerillamail.org",
                "guerrillamail.biz",
                "guerrillamail.com",
                "guerrillamail.de",
                "guerrillamail.net",
                "guerrillamail.org",
                "guerrillamailblock.com",
                "hacccc.com",
                "haltospam.com",
                "herp.in",
                "hidzz.com",
                "hochsitze.com",
                "hotmil.com",
                "hotpop.com",
                "hulapla.de",
                "hushmail.com",
                "ieatspam.eu",
                "ieatspam.info",
                "imails.info",
                "incognitomail.com",
                "incognitomail.net",
                "incognitomail.org",
                "instant-mail.de",
                "internet.com",
                "ipoo.org",
                "irish2me.com",
                "jetable.com",
                "jetable.fr.nf",
                "jetable.net",
                "jetable.org",
                "jsrsolutions.com",
                "junk1e.com",
                "jnxjn.com",
                "kasmail.com",
                "klassmaster.com",
                "klzlk.com",
                "kulturbetrieb.info",
                "kurzepost.de",
                "lavabit.com",
                "letthemeatspam.com",
                "lhsdv.com",
                "lifebyfood.com",
                "litedrop.com",
                "lookugly.com",
                "lr78.com",
                "lroid.com",
                "m4ilweb.info",
                "mail.by",
                "mail114.net",
                "mail4trash.com",
                "mailbucket.org",
                "mailcatch.com",
                "maileater.com",
                "mailexpire.com",
                "mailguard.me",
                "mail-filter.com",
                "mailin8r.com",
                "mailinator.com",
                "mailinator.net",
                "mailinator.org",
                "mailinator.us",
                "mailinator2.com",
                "mailme.lv",
                "mailmetrash.com",
                "mailmoat.com",
                "mailnator.com",
                "mailnesia.com",
                "mailnull.com",
                "mailquack.com",
                "mailscrap.com",
                "mailzilla.org",
                "makemetheking.com",
                "manybrain.com",
                "mega.zik.dj",
                "meltmail.com",
                "mierdamail.com",
                "migumail.com",
                "mintemail.com",
                "mbx.cc",
                "mobileninja.co.uk",
                "moburl.com",
                "moncourrier.fr.nf",
                "monemail.fr.nf",
                "monmail.fr.nf",
                "mt2009.com",
                "myemailboxy.com",
                "mymail-in.net",
                "mypacks.net",
                "mypartyclip.de",
                "mytempemail.com",
                "mytrashmail.com",
                "nepwk.com",
                "nervmich.net",
                "nervtmich.net",
                "nice-4u.com",
                "no-spam.ws",
                "nobulk.com",
                "noclickemail.com",
                "nogmailspam.info",
                "nomail.xl.cx",
                "nomail2me.com",
                "none.com",
                "none.net",
                "nospam.ze.tc",
                "nospam4.us",
                "nospamfor.us",
                "nospamthanks.info",
                "notmailinator.com",
                "nowhere.org",
                "nowmymail.com",
                "nwldx.com",
                "objectmail.com",
                "obobbo.com",
                "onewaymail.com",
                "otherinbox.com",
                "owlpic.com",
                "pcusers.otherinbox.com",
                "pepbot.com",
                "poczta.onet.pl",
                "politikerclub.de",
                "pookmail.com",
                "privy-mail.com",
                "proxymail.eu",
                "prtnx.com",
                "putthisinyourspamdatabase.com",
                "qq.com",
                "quickinbox.com",
                "rcpt.at",
                "recode.me",
                "regbypass.com",
                "rmqkr.net",
                "royal.net",
                "rppkn.com",
                "rtrtr.com",
                "s0ny.net",
                "safe-mail.net",
                "safetymail.info",
                "safetypost.de",
                "sample.com",
                "sample.net",
                "sample.org",
                "saynotospams.com",
                "sandelf.de",
                "schafmail.de",
                "selfdestructingmail.com",
                "sendspamhere.com",
                "sharklasers.com",
                "shitmail.me",
                "shitware.nl",
                "sinnlos-mail.de",
                "siteposter.net",
                "skeefmail.com",
                "slopsbox.com",
                "smellfear.com",
                "snakemail.com",
                "sneakemail.com",
                "snkmail.com",
                "sofort-mail.de",
                "sogetthis.com",
                "spam.la",
                "spam.su",
                "spam4.me",
                "spamavert.com",
                "spambob.net",
                "spambob.org",
                "spambog.com",
                "spambog.de",
                "spambox.info",
                "spambog.ru",
                "spambox.us",
                "spamcero.com",
                "spamday.com",
                "spamex.com",
                "spamfree24.com",
                "spamfree24.de",
                "spamfree24.eu",
                "spamfree24.info",
                "spamfree24.net",
                "spamfree24.org",
                "spamfree.eu",
                "spamgourmet.com",
                "spamherelots.com",
                "spamhereplease.com",
                "spamhole.com",
                "spamify.com",
                "spaminator.de",
                "spamkill.info",
                "spaml.com",
                "spaml.de",
                "spammotel.com",
                "spamobox.com",
                "spamsalad.in",
                "spamspot.com",
                "spamthis.co.uk",
                "spamthisplease.com",
                "spamtroll.net",
                "speed.1s.fr",
                "spoofmail.de",
                "squizzy.de",
                "stinkefinger.net",
                "stuffmail.de",
                "supergreatmail.com",
                "superstachel.de",
                "suremail.info",
                "tagyourself.com",
                "talkinator.com",
                "tapchicuoihoi.com",
                "teewars.org",
                "teleworm.com",
                "teleworm.us",
                "temp.emeraldwebmail.com",
                "tempalias.com",
                "tempe-mail.com",
                "tempemail.biz",
                "tempemail.co.za",
                "tempemail.com",
                "tempemail.net",
                "tempinbox.co.uk",
                "tempinbox.com",
                "tempmaildemo.com",
                "tempmail.it",
                "tempomail.fr",
                "temporaryemail.net",
                "temporaryemail.us",
                "temporaryinbox.com",
                "tempthe.net",
                "test.com",
                "test.net",
                "thanksnospam.info",
                "thankyou2010.com",
                "thisisnotmyrealemail.com",
                "throwawayemailaddress.com",
                "tittbit.in",
                "tmailinator.com",
                "tradermail.info",
                "trash2009.com",
                "trash2010.com",
                "trash2011.com",
                "trash-amil.com",
                "trash-mail.at",
                "trash-mail.com",
                "trash-mail.de",
                "trashmail.at",
                "trashmail.com",
                "trashmail.me",
                "trashmail.net",
                "trashmail.ws",
                "trashymail.com",
                "trashymail.net",
                "tyldd.com",
                "umail.net",
                "uggsrock.com",
                "uroid.com",
                "veryrealemail.com",
                "vidchart.com",
                "vubby.com",
                "webemail.me",
                "webm4il.info",
                "weg-werf-email.de",
                "wegwerf-email-addressen.de",
                "wegwerf-emails.de",
                "wegwerfadresse.de",
                "wegwerfemail.de",
                "wegwerfmail.de",
                "wegwerfmail.info",
                "wegwerfmail.net",
                "wegwerfmail.org",
                "whatiaas.com",
                "whatsaas.com",
                "wh4f.org",
                "whyspam.me",
                "willselfdestruct.com",
                "winemaven.info",
                "wuzupmail.net",
                "yaho.com",
                "yahoo.com.ph",
                "yahoo.com.vn",
                "yeah.net",
                "yogamaven.com",
                "yopmail.com",
                "yopmail.fr",
                "yopmail.net",
                "yuurok.com",
                "xoxy.net",
                "xyzfree.net",
                "za.com",
                "zippymail.info",
                "zoemail.net",
                "zomg.info"
            });
    }
}
