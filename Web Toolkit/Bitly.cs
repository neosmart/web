using System.Linq;
using System.Xml.Linq;

namespace NeoSmart.Web
{
	public static class Bitly
	{
	    private static string _apiKey;
	    private static string _login;

        public static void SetCredentials(string apiKey, string login)
        {
            _apiKey = apiKey;
            _login = login;
        }

		public static BitlyResult ShortenUrl (string longUrl)
		{
			var url = string.Format(
				"http://api.bit.ly/shorten?format=xml&version=2.0.1&longUrl={0}&login={1}&apiKey={2}", 
				System.Uri.EscapeUriString(longUrl), 
				_login, 
				_apiKey
				);
		
			var resultXml = XDocument.Load(url);
			var x = (from result in resultXml.Descendants("nodeKeyVal")
			         select new BitlyResult
			                	{
			                		UserHash = result.Element("userHash").Value,
			                		ShortUrl = result.Element("shortUrl").Value
			                	}
			        );
			return x.Single();
		}
	}

	public class BitlyResult
	{
		public string UserHash { get; set; }
		public string ShortUrl { get; set; }
	}
}
