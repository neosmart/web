using DnsClient;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Microsoft.Extensions.Logging;
using NeoSmart.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NeoSmart.Web
{
    public partial class EmailFilter
    {
        public static ILogger<EmailFilter>? Logger;

        public enum BlockReason
        {
            /// The format of the email address did not conform to that of a public email account.
            /// (This isn't the same as what is technically allowed.)
            InvalidFormat,
            /// The MX domain could not be resolved or resolved to an invalid value.
            InvalidMx,
            /// The email fell afoul of one or more statically defined checks, such as known typo
            /// domains or user portion not consistent with domain mail server's published rules,
            /// such as minimum length or allowed characters.
            StaticRules,
            /// Either the domain itself is blacklisted or its MX resolved to a blacklisted value
            Blacklisted,
            /// The user/domain combination tripped one or more heuristic filters that indicate a
            /// (high) probability of being a fake email.
            Heuristic,
            /// <summary>
            /// The domain is likely a typo of a top-100 address.
            /// </summary>
            LikelyTypo,
        }
        [GeneratedRegex(@"^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();
        [GeneratedRegex(@"^[0-9]+$", RegexOptions.IgnoreCase)]
        private static partial Regex NumericEmailRegex();
        [GeneratedRegex(@"^[0-9]+\.[^.]+$", RegexOptions.IgnoreCase)]
        private static partial Regex NumericDomainRegex();
        [GeneratedRegex(@"\.(cm|cmo|om|comm|con|coom|ccom|comn|c0m|lcom|ent)$", RegexOptions.IgnoreCase)]
        private static partial Regex MistypedTldRegex();
        [GeneratedRegex(@"\.(ru|cn|info|tk)$", RegexOptions.IgnoreCase)]
        private static partial Regex TldRegex();
        [GeneratedRegex(@"\*|^a+b+c+|address|bastard|bitch|blabla|d+e+f+g+|example|fake|fuck|junk|junk|^lol$| (a|no|some)name|no1|nobody|none|noone|nope|nothank|noway|qwerty|sample|spam|suck|test|thanks|^user$|whatever|^x+y+z+", RegexOptions.IgnoreCase)]
        private static partial Regex ExpressionRegex();
        [GeneratedRegex(@"^[asdfghjkvlxm]+$", RegexOptions.IgnoreCase)]
        private static partial Regex QwertyRegex();
        [GeneratedRegex(@"^[asdfghjkvlx]+\.[^.]+$", RegexOptions.IgnoreCase)]
        private static partial Regex QwertyDomainRegex();
        [GeneratedRegex(@"(.)(:?\1){3,}|^(.)\3+$?$", RegexOptions.IgnoreCase)]
        private static partial Regex RepeatedCharsRegex();

        private static HashSet<IPAddress> BlockedMxAddresses = new HashSet<IPAddress>();
        private static TaskCompletionSource ReverseDnsCompleteEvent = new();
        private static bool ReverseDnsComplete = false;

        static EmailFilter()
        {
            ValidMxDomainCache = new(TopDomains, StringComparer.OrdinalIgnoreCase);

            Task.Run(static async () =>
            {
                // Create a set of IP addresses for known bad domains used to reverse filter any future MX lookups for a match.
                // This will catch aliases for temporary email address services.
                var blockedMxAddresses = new ConcurrentBag<IPAddress>();
                var mxTasks = MxBlackList.Select(async domain =>
                {
                    try
                    {
                        var mxResults = await DnsLookup.GetMXRecordsAsync(domain);
                        var addresses = Task.WhenEach(mxResults.Select(result => DnsLookup.GetIpAddressesAsync(result).AsTask()));
                        await foreach (var block in addresses)
                        {
                            foreach (var ip in await block)
                            {
                                blockedMxAddresses.Add(ip);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "Error retrieving MX info for domain {MxBlackListDomain}", domain);
                    }
                });

                await Task.WhenAll(mxTasks);
                foreach (var address in blockedMxAddresses)
                {
                    BlockedMxAddresses.Add(address);
                }
                ReverseDnsComplete = true;
                ReverseDnsCompleteEvent.SetResult();
            });
        }

        // aka IsDefinitelyFakeEmail
        public static bool IsFakeEmail(string email)
        {
            return IsProbablyFakeEmail(email, 0, true);
        }

        public static bool HasValidMx(MailAddress address)
        {
            try
            {
                if (!ReverseDnsComplete)
                {
                    using var task = ReverseDnsCompleteEvent.Task;
                    ReverseDnsCompleteEvent.Task.Wait();
                }

                if (ValidMxDomainCache.Contains(address.Host))
                {
                    return true;
                }

                var mxRecords = DnsLookup.GetMXRecords(address.Host);
                if (!mxRecords.Any())
                {
                    // No MX record associated with this address or timeout
                    Logger?.LogInformation("Could not find MX record for domain {MailDomain}", address.Host);
                    return false;
                }

                // Compare against our blacklist
                foreach (var record in mxRecords)
                {
                    var addresses = DnsLookup.GetIpAddresses(record);
                    if (addresses.Any(BlockedMxAddresses.Contains))
                    {
                        // This mx record points to the same IP as a blacklisted MX record or timeout
                        Logger?.LogInformation("Email domain {MailDomain} has MX record {MxRecord} in blacklist!",
                            address.Host, record);
                        return false;
                    }
                }

                lock (ValidMxDomainCache)
                {
                    ValidMxDomainCache.Add(address.Host);
                }
            }
            catch (DnsResponseException ex)
            {
                Logger?.LogWarning(ex, "Error looking up MX records for {MxDomain}", address.Host);
                // Err on the side of caution
                return true;
            }

            return true;
        }

        public static async ValueTask<bool> HasValidMxAsync(MailAddress address)
        {
            try
            {
                if (!ReverseDnsComplete)
                {
                    using var task = ReverseDnsCompleteEvent.Task;
                    await task;
                }

                if (ValidMxDomainCache.Contains(address.Host))
                {
                    return true;
                }

                var mxRecords = await DnsLookup.GetMXRecordsAsync(address.Host);
                if (!mxRecords.Any())
                {
                    // No MX record associated with this address or timeout
                    Logger?.LogInformation("Could not find MX record for domain {MailDomain}", address.Host);
                    return false;
                }

                // Compare against our blacklist
                foreach (var record in mxRecords)
                {
                    var addresses = await DnsLookup.GetIpAddressesAsync(record);
                    if (addresses.Any(BlockedMxAddresses.Contains))
                    {
                        // This mx record points to the same IP as a blacklisted MX record or timeout
                        Logger?.LogInformation("Email domain {MailDomain} has MX record {MxRecord} in blacklist!",
                            address.Host, record);
                        return false;
                    }
                }

                lock (ValidMxDomainCache)
                {
                    ValidMxDomainCache.Add(address.Host);
                }
            }
            catch (DnsResponseException ex)
            {
                Logger?.LogWarning(ex, "Error looking up MX records for {MxDomain}", address.Host);
                // Err on the side of caution
                return true;
            }

            return true;
        }

        static public bool HasValidMx(string email)
        {
            if (!MailAddress.TryCreate(email, out var address))
            {
                return false;
            }
            return HasValidMx(address);
        }

        static public async ValueTask<bool> HasValidMxAsync(string email)
        {
            if (!MailAddress.TryCreate(email, out var address))
            {
                return false;
            }
            return await HasValidMxAsync(address);
        }

        private static bool IsProbablyFakeEmailInner(string? email, int meanness, [NotNullWhen(false)] out MailAddress? mailAddress)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                mailAddress = null;
                return true;
            }

            // Instead of making all the regex rules case-insensitive
            email = email.ToLower();
            if (!IsValidFormat(email))
            {
                mailAddress = null;
                return true;
            }

            mailAddress = new MailAddress(email);

            if (meanness >= 0)
            {
                if (DomainMinimumPrefix.TryGetValue(mailAddress.Host, out var minimumPrefix)
                    && minimumPrefix > mailAddress.User.Length)
                {
                    return true;
                }
                if (MistypedTldRegex().IsMatch(mailAddress.Host))
                {
                    return true;
                }
                if (TypoDomains.Contains(mailAddress.Host))
                {
                    return true;
                }
            }
            if (meanness >= 1)
            {
                if (BlockedDomains.Contains(mailAddress.Host))
                {
                    return true;
                }
            }
            if (meanness >= 2)
            {
                if (ExpressionRegex().IsMatch(mailAddress.User))
                {
                    return true;
                }
            }
            if (meanness >= 4)
            {
                if (RepeatedCharsRegex().IsMatch(mailAddress.User) ||
                    RepeatedCharsRegex().IsMatch(mailAddress.Host))
                {
                    return true;
                }
            }
            if (meanness >= 5)
            {
                if (NumericEmailRegex().IsMatch(mailAddress.User))
                {
                    return true;
                }
                if (ExpressionRegex().IsMatch(email))
                {
                    return true;
                }
            }
            if (meanness >= 6)
            {
                if (QwertyRegex().IsMatch(mailAddress.User))
                {
                    return true;
                }
                if (QwertyDomainRegex().IsMatch(mailAddress.Host))
                {
                    return true;
                }
                if (NumericDomainRegex().IsMatch(mailAddress.Host))
                {
                    return true;
                }
            }
            if (meanness >= 7)
            {
                // This is including the tld, so 3 is insanely generous.
                // 2 letters + period + 3 tld = 6
                if (mailAddress.Host.Length < 6)
                {
                    return true;
                }
            }
            if (meanness >= 8)
            {
                if (mailAddress.User.Length < 3)
                {
                    return true;
                }
                if (HasMaybeMistypedDomain(mailAddress))
                {
                    return true;
                }
            }
            if (meanness >= 9)
            {
                if (mailAddress.User.Length < 5)
                {
                    return true;
                }
            }
            if (meanness >= 10)
            {
                if (TldRegex().IsMatch(mailAddress.Host))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsProbablyFakeEmail(string? email, int meanness, bool validateMx = false)
        {
            if (IsProbablyFakeEmailInner(email, meanness, out var mailAddress))
            {
                return true;
            }

            // Do this last because it's the most expensive
            if (validateMx && !HasValidMx(mailAddress))
            {
                return true;
            }

            return false;
        }

        public static async ValueTask<bool> IsProbablyFakeEmailAsync(string? email, int meanness, bool validateMx = false)
        {
            if (IsProbablyFakeEmailInner(email, meanness, out var mailAddress))
            {
                return true;
            }

            // Do this last because it's the most expensive
            if (validateMx && !await HasValidMxAsync(mailAddress))
            {
                return true;
            }

            return false;
        }

        public static bool HasMaybeMistypedDomain(string email)
        {
            MailAddress mailAddress;
            try
            {
                mailAddress = new MailAddress(email.ToLowerInvariant().Trim());
            }
            catch(Exception ex)
            {
                Logger?.LogDebug(ex, "Error parsing provided email {InputEmail} to MailAddress", email);
                return false;
            }

            return HasMaybeMistypedDomain(mailAddress);
        }

        public static bool HasMaybeMistypedDomain(MailAddress mailAddress)
        {
            return HasMaybeMistypedDomain(mailAddress, out _, out _);
        }

        private static readonly INormalizedStringSimilarity StringSimilarity = new NormalizedLevenshtein();
        public static bool HasMaybeMistypedDomain(MailAddress mailAddress,
            [NotNullWhen(true)] out MailAddress? corrected,
            [NotNullWhen(true)] out double? similarity,
            double threshold = 0.75)
        {
            var domain = mailAddress.Host.ToLowerInvariant();
            if (TopDomains.Contains(domain))
            {
                corrected = null;
                similarity = null;
                return false;
            }

            string bestMatch = "";
            similarity = 0.0;
            foreach (var topDomain in TopDomains)
            {
                var s = StringSimilarity.Similarity(domain, topDomain) * Math.Min(topDomain.Length, domain.Length) / Math.Max(topDomain.Length, domain.Length);
                if (s > similarity)
                {
                    bestMatch = topDomain;
                    similarity = s;
                }
            }

            if (similarity > threshold)
            {
                // This is extremely similar to a popular email domain and isn't itself a popular email domain.
                corrected = new MailAddress($"{mailAddress.User}@{bestMatch}", mailAddress.DisplayName);
                return true;
            }

            corrected = null;
            return false;
        }

        private static readonly IdnMapping IdnMapping = new();
        [GeneratedRegex(@"(@)(.+)$", RegexOptions.CultureInvariant)]
        private static partial Regex EmailPartsRegex();

        public static bool IsValidFormat(string email)
        {
            bool invalid = false;
            if (string.IsNullOrEmpty(email))
            {
                return false;
            }

            try
            {
                email = EmailPartsRegex().Replace(email, match =>
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    string domainName = match.Groups[2].Value;
                    try
                    {
                        domainName = IdnMapping.GetAscii(domainName);
                    }
                    catch (ArgumentException)
                    {
                        invalid = true;
                    }

                    return match.Groups[1].Value + domainName;
                });
            }
            catch (RegexMatchTimeoutException ex)
            {
                Logger?.LogWarning(ex, $"Timeout evaluating email {{InputEmail}} with {nameof(EmailPartsRegex)} regular expression!", email);
                return false;
            }

            if (invalid)
            {
                return false;
            }

            try
            {
                // return true if input is in valid e-mail format.
                return EmailRegex().IsMatch(email);
            }
            catch (RegexMatchTimeoutException ex)
            {
                Logger?.LogWarning(ex, $"Timeout evaluating email {{InputEmail}} with {nameof(EmailRegex)} regular expression!", email);
                return false;
            }
        }

        // These domains will never fail an MX check. Using a HashSet and not a SortedList
        // because this list grows over time. Initialized in static constructor.
        private static readonly HashSet<string> ValidMxDomainCache;

        /// <summary>
        /// These domains have (or had) valid MX records but are still to be considered typos.
        /// </summary>
        public static readonly SortedList<string> TypoDomains = new()
        {
            // Variations for yahoo.com
            "ahoo.com",
            "yahho.com",
            "yahool.com",
            "yahooo.com",
            "yaoo.com",

            // Variations for gmail.com
            "gail.com",
            "gamail.com",
            "gamil.com",
            "gmial.com",
            "gmil.com",
            "gmsil.com",
            "gnail.com",
            "gol.com",
            "gmail.con",
            "gmail.co",
            "gmail.om",
            "gmal.com",
            "gml.com",

            // Variations for hotmail.com
            "homail.com",
            "homtail.com",
            "hotmal.com",
            "hotmsil.co",
            "hotmsil.com",
            "otmail.com",
            "hotmil.com",

            // Variations for live.com
            "ive.com",
            "lve.com",
            "liv.com",
            "live.co",

            // Variations for outlook.com
            "outlok.com",
            "outllok.com",
            "outloo.com",
            "otlook.com",

            // Variations for comcast.net
            "comast.net",
            "comcost.net",
            "comcat.net",
            "comcst.net",

            // Variations for verizon.net
            "verizion.net",
            "verrizon.net",
            "verison.net",
            "vrizon.net",

            // Variations for icloud.com
            "iclod.com",
            "icluod.com",
            "iloud.com",
            "icoud.com",

            // Variations for aol.com
            "aol.co",
            "al.com",
            "aol.om",
            "aool.com",
        };

        // Domains that have hard rules as to the length of the prefix (prefix@domain)
        private static readonly SortedList<string, int> DomainMinimumPrefix = new()
        {
            { "gmail.com", 6 },
            { "googlemail.com", 6 },
            { "yahoo.com", 4 },
        };

        private static readonly SortedList<string> MxBlackList = new(
        [
            "mvrht.com", // 10minutemail.com
            "mailinator.com",
            "sharklasers.com", // guerrillamail.com
            "teleworm.us", // fakemailgenerator.com
            "hmamail.com", // hidemyass email
            "generator.email", // primary web address and mx record for many different domains
            "tempr.email", // discard.email many different domains
        ]);

        private static readonly SortedList<string> TopDomains = new()
        {
            "gmail.com",
            "yahoo.com",
            "hotmail.com",
            "aol.com",
            "hotmail.co.uk",
            "hotmail.fr",
            "msn.com",
            "yahoo.fr",
            "wanadoo.fr",
            "orange.fr",
            "comcast.net",
            "yahoo.co.uk",
            "yahoo.com.br",
            "yahoo.co.in",
            "live.com",
            "rediffmail.com",
            "free.fr",
            "gmx.de",
            "web.de",
            "yandex.ru",
            "ymail.com",
            "libero.it",
            "outlook.com",
            "uol.com.br",
            "bol.com.br",
            "mail.ru",
            "cox.net",
            "hotmail.it",
            "sbcglobal.net",
            "sfr.fr",
            "live.fr",
            "verizon.net",
            "live.co.uk",
            "googlemail.com",
            "yahoo.es",
            "ig.com.br",
            "live.nl",
            "bigpond.com",
            "terra.com.br",
            "yahoo.it",
            "neuf.fr",
            "yahoo.de",
            "alice.it",
            "rocketmail.com",
            "att.net",
            "laposte.net",
            "facebook.com",
            "bellsouth.net",
            "yahoo.in",
            "hotmail.es",
            "charter.net",
            "yahoo.ca",
            "yahoo.com.au",
            "rambler.ru",
            "hotmail.de",
            "tiscali.it",
            "shaw.ca",
            "yahoo.co.jp",
            "sky.com",
            "earthlink.net",
            "optonline.net",
            "freenet.de",
            "t-online.de",
            "aliceadsl.fr",
            "virgilio.it",
            "home.nl",
            "qq.com",
            "telenet.be",
            "me.com",
            "yahoo.com.ar",
            "tiscali.co.uk",
            "yahoo.com.mx",
            "voila.fr",
            "gmx.net",
            "mail.com",
            "planet.nl",
            "tin.it",
            "live.it",
            "ntlworld.com",
            "arcor.de",
            "yahoo.co.id",
            "frontiernet.net",
            "hetnet.nl",
            "live.com.au",
            "yahoo.com.sg",
            "zonnet.nl",
            "club-internet.fr",
            "juno.com",
            "optusnet.com.au",
            "blueyonder.co.uk",
            "bluewin.ch",
            "skynet.be",
            "sympatico.ca",
            "windstream.net",
            "mac.com",
            "centurytel.net",
            "chello.nl",
            "live.ca",
            "aim.com",
            "bigpond.net.au",
        };

        // Originally from http://www.digitalfaq.com/forum/web-tech/5050-throwaway-email-block.html
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
                "anymail.com",
                "armyspy.com",
                "beefmilk.com",
                "bigstring.com",
                "binkmail.com",
                "bio-muesli.net",
                "bob.com",
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
                "com.com",
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
                "mail.com",
                "mail.net",
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
                "net.net",
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
                "q1.com",
                "qa.com",
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
                "spam.com",
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
                "www.com",
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
