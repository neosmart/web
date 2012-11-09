NeoSmart Technologies Web Toolkit
===

The NeoSmart Technologies .NET Web Toolkit is a collection of classes, routines, and methods that we have found useful in the development of ASP.NET (MVC) projects.

It is an open-source (MIT-licensed) library. It contains a mostly-random collection of code, i.e. it does not have any single unifying purpose other than making your life developing ASP.NET web applications easier. Hence, it is called a "toolkit" and not a "framework."

The Web Toolkit uses nuget to manage package references. It is not yet available via nuget, however (soon, soon!).

Feel free to fork and submit pull requests as needed. We especially welcome improvements to existing features, and are also open to adding new classes and methods if they fit with the general "theme" of this toolkit.

All code is in the `NeoSmart.Web` namespace, and the class reference is as follows:

NeoSmart.Web.Seo
---

This class serves to resolve one of our biggest gripes with the ASP.NET/IIS/Windows platform in general: case insensitivity. The web is a case-sensitive platform. As RFC 2616 states, [web URIs *are* case-sensitive](http://www.w3.org/Protocols/rfc2616/rfc2616-sec3.html#sec3.2.3), but Microsoft eschews this standard for the Windows default of case-insensitive names for all resources, including web URIs. The problem with this is that you can be heavily penalized by search engines (esp. Google) for having duplicate content: multiple links (same text, but different cases) will resolve to the same location.

`NeoSmart.Web.Seo` will automatically redirect incorrectly-cased requests to the appropriate location with a `301 Permanent Redirect` header to maximize the SEO and pagerank for the correct links.

###SeoRedirect

`public static void SeoRedirect(Controller controller)`

This is the only method in the `SeoRedirect` class, and should be used to initiate a redirect for any HTTP GET requests that you wish to be coalesced to a single URL.

The redirection will point to the URL using the same case as the controller class name and the action method name in your code file. e.g. For a action method `HomeController.SamplePage`, requests to anything other than `/Home/SamplePage` *with that same case* will be redirected to `/Home/SamplePage`, including requests for `/home/samplepage`, `/home/samPLEpAge`, etc.

Usage is quite simple: just copy-and-paste the following line to the start of every method you desire to be SEO-redirected:

```NeoSmart.Web.Seo.SeoRedirect(this)```

The `SeoRedirect` class will automatically use a combination of reflection and C# 5.0's [Caller Info attributes](http://msdn.microsoft.com/en-us/library/hh534540.aspx) to deduce all the information needed to determine the correct link. Do not worry about the performance overhead of reflection - the stack trace is only obtained once, the correct controller and action info is then cached in memory for later retrieval, i.e. only the first request for any method incurs a reflection overhead.

Note: do *not* use `SeoRedirect` in actions that will be called via HTTP POST. The HTTP RFC states that POST contents are *not* resubmitted on redirect, i.e. any redirected POST requests will lose POST data, and you'll get a blank request!

In addition to normalizing case, `SeoRedirect` will also strip remove superflous `/Index/` references, *remove* trailing backslashes from requests to the "directory" page (method without parameters) and *add* trailing backslashes to non-directory requests (methods with parameters).

Example: for a given directory, index action `SoftwareController.Index()`, `SeoRedirect` will convert any of the following links to the correct URI `http://example.com/Software/`:

* `http://example.com/Software/Index/`
* `http://example.com/Software/Index`
* `http://example.com/Software/Index/`
* `http://example.com/Software/index/`
* `http://example.com/software`
* `http://example.com/Software`
* `http://example.com/SoFTwAre/INDEx/`
* etc.

Example: for a given action `SoftwareController.Something(id)`, `SeoRedirect` will convert any of the following links to the correct URI `http://example.com/Sofware/Something/Parameter` (whatever parameter might be):

* `http://example.com/Software/Something/Parameter/`
* `http://example.com/Software/Something/parameter/`
* `http://example.com/Software/something/parameter`
* etc.

NeoSmart.Web.ScopedMutex
---
The `ScopedMutex` object is a hybrid between a named `Mutex` and a `lock` block. Unlike a `lock`, instead of locking on an object, it locks on a unique identifier (GUID, usually). Unlike a named `Mutex`, the lock is automatically released in case of exception, request abortion, etc.

Since it uses a GUID, it will block across sessions and page requests, and will be "deleted" when either all references to it cease to exist, or the ASP.NET application is restarted. It is intended to be used almost exclusively in `using` blocks.

* `ScopedMutex(string name)`
* `public bool WaitOne()`
* `public void ReleaseMutex()`
* `public void Dispose()`

The `ScopedMutex` constructor is where the unique lock/mutex name is specified in the form of a string. It can only be set in the constructor (i.e. the name cannot be set nor changed at a later time).

Usage is very simple:

```using (var mutex = new ScopedMutex("myguid"))
			{
				mutex.WaitOne();
				//Do something here
				mutex.ReleaseMutex();
			}```

As you can see, the `ScopedMutex` must be explicitly locked with `ScopedMutex.WaitOne()`. To prevent confusion, there is no parameter to lock on creation in the constructor as there is in the `Mutex` class.

Should the code terminate or throw an exception for any reason in the middle of the `using` block before `mutex.ReleaseMutex()` is called, the `Dispose` method will automatically release the mutex and make it available for other threads/requests. This holds true regardless of whether the request is terminated due to user abort, an exception in the `//Do something here` block, etc.

It is important to note that `ScopedMutex` does not throw an `AbandonedMutexException`: if another thread/request holding the `ScopedMutex` with the same GUID aborts in the middle, the current `WaitOne()` will succeed without notice that anything went wrong. Given that the `ScopedMutex` object is meant for request synchronization, there is little benefit to requiring everyone to handle `AbandonedMutexException` themselves.

The usage of `ReleaseMutex()` in the above code sample is extraneous. It serves zero purpose other than clearly specifying the intended behavior. Upon reaching the end of the `using` block, if `WaitOne()` had been previously called without a subsequent call to `ReleaseMutex()`, `ReleaseMutex()` will be called automatically, as one would expect.

NeoSmart.Web.Bitly
---

This static class is a very simple wrapper around the Bitly API for URL shortening.

* `public static void SetCredentials(string apiKey, string login)`
* `public static BitlyResult ShortenUrl (string longUrl)`

The `SetCredentials` call should be made before any `ShortenUrl` calls are used. The parameters `apiKey` and `login` should be obtained from your Bitly account. As this is a static class, this data will be saved for the lifetime of your ASP.NET application, however, the credentials may be overridden via subsequent calls to `SetCredentials`

We recommend placing the call to `SetCredentials` in the `Global.asax.cs` file in the `Application_Start()` method:

`NeoSmart.Web.S3.SetCredentials("redacted apiKey", "redacted login");`

The result of calling `Bitly.ShortenUrl(urlToBeShortened)` is a `BitlyResult` structure:

```public class BitlyResult
{
	public string UserHash { get; set; }
	public string ShortUrl { get; set; }
}```

The shortened URL is returned in `BitlyResult.ShortUrl`. The `BitlyResult.UserHash` field contains only the hash portion of the shortened URL (for use with alternate Bitly domains, such as j.mp, or for URI statistics-querying purposes).

NeoSmart.Web.FraudControl
---

This class was made to rate-limit attempts at charging credit cards to make your eCommerce website a non-viable credit card testing destination for credit card thieves. Credit card framing is a huge operation, and usually thieves will sell in bulk the credit card numbers to a middle man who then tests each and every card to determine whether or not it has been reported stolen.

To do so, they generally prey on smaller online businesses, taking advantage of the fact that few such sites limit the number of unique credit cards per IP address. The `FraudControl` class limits each remote IP to a maximum number of unique credit cards, by default set to 3.

* `public static int MaxCardsPerIp`
* `public static bool ValidatePurchase(HttpRequestBase request, string cardFingerprint, bool throwException = true)`

This is another static class that stores previous requset data in long-running dictionaries. It is inteneded to be called from within a controller action method:

`ValidatePurchase(Request, "card fingerprint or card number", true/false)`

The card fingerprint field is a hash that uniquely identifies the card processed. The `throwException` parameter determines the behavior of the method: it either returns false on failure (maximum cards per remote IP exceeded) or throws an exception of type `FraudulentPurchaseException` deriving from `System.Exception`.