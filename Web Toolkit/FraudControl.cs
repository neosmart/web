using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NeoSmart.Web
{
    internal class ClientProfile
    {
        public int PurchaseAttempts = 0;
        public List<string> Cards = new List<string>();
    }

    public class FraudulentPurchaseException : Exception
    {
        public FraudulentPurchaseException()
            : base("Payment failed due to fraud filter indicators. Please try a different payment method or contact sales support for help.")
        {
        }
    }

    public static class FraudControl
    {
        private static int _maxCards = 3;
        private static readonly Dictionary<string, ClientProfile> ClientHistory = new Dictionary<string, ClientProfile>(); //ip is key
        private static readonly HashSet<string> BannedRemotes = new HashSet<string>();

        public static IEnumerable<string> BannedAddresses => BannedRemotes;

        public static int MaxCardsPerIp
        {
            get { return _maxCards; }
            set { _maxCards = value; }
        }

        public static bool ValidatePurchase(HttpRequest request, string cardFingerprint, bool throwException = true)
        {
            if (string.IsNullOrWhiteSpace(cardFingerprint))
            {
                return true;
            }

            string remote;
            if (!Utils.GetClientIpAddress(request, out remote))
            {
                //Nothing we can do here
                return true;
            }

            ClientProfile client;
            if (!ClientHistory.TryGetValue(remote, out client))
            {
                client = new ClientProfile();
                ClientHistory.Add(remote, client);
            }

            ++client.PurchaseAttempts;

            if (!client.Cards.Contains(cardFingerprint))
            {
                client.Cards.Add(cardFingerprint);
            }

            if (client.Cards.Count > MaxCardsPerIp || BannedRemotes.Contains(remote))
            {
                if (BannedRemotes.Contains(remote) == false)
                {
                    BannedRemotes.Add(remote);
                }
                if (throwException)
                {
                    throw new FraudulentPurchaseException();
                }

                return false;
            }

            return true;
        }
    }
}
