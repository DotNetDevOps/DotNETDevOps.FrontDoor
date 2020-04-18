using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    /// <summary>
    /// MSAL token cache provider interface.
    /// </summary>
    public interface IMsalTokenCacheProvider
    {
        /// <summary>
        /// Initializes a token cache (which can be a user token cache or an app token cache)
        /// </summary>
        /// <param name="tokenCache">Token cache for which to initialize the serialization</param>
        /// <returns></returns>
        void Initialize(ITokenCache tokenCache);

        /// <summary>
        /// Clear the cache
        /// </summary>
        /// <returns></returns>
        Task ClearAsync();
    }

    public abstract class MsalAbstractTokenCacheProvider : IMsalTokenCacheProvider
    {


        /// <summary>
        /// Initializes the token cache serialization.
        /// </summary>
        /// <param name="tokenCache">Token cache to serialize/deserialize</param>
        /// <returns></returns>
        public void Initialize(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccessAsync(OnBeforeAccessAsync);
            tokenCache.SetAfterAccessAsync(OnAfterAccessAsync);
            tokenCache.SetBeforeWriteAsync(OnBeforeWriteAsync);


        }

        /// <summary>
        /// Cache key
        /// </summary>
        //private string GetCacheKey(bool isAppTokenCache)
        //{
        //    if (isAppTokenCache)
        //    {
        //        return $"{_azureAdOptions.Value.ClientId}_AppTokenCache";
        //    }
        //    else
        //    {
        //        return _httpContextAccessor.HttpContext.User.GetMsalAccountId();
        //        // In the case of Web Apps, the cache key is the user account Id, and the expectation is that AcquireTokenSilent
        //        // should return a token otherwise this might require a challenge
        //        // In the case Web APIs, the token cache key is a hash of the access token used to call the Web API
        //       // JwtSecurityToken jwtSecurityToken = _httpContextAccessor.HttpContext.GetTokenUsedToCallWebAPI();
        //       // return (jwtSecurityToken != null) ? jwtSecurityToken.RawSignature
        //         //                                                 : _httpContextAccessor.HttpContext.User.GetMsalAccountId();
        //    }
        //}

        /// <summary>
        /// Raised AFTER MSAL added the new token in its in-memory copy of the cache.
        /// This notification is called every time MSAL accessed the cache, not just when a write took place:
        /// If MSAL's current operation resulted in a cache change, the property TokenCacheNotificationArgs.HasStateChanged will be set to true.
        /// If that is the case, we call the TokenCache.SerializeMsalV3() to get a binary blob representing the latest cache content – and persist it.
        /// </summary>
        /// <param name="args">Contains parameters used by the MSAL call accessing the cache.</param>
        private async Task OnAfterAccessAsync(TokenCacheNotificationArgs args)
        {

            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                //  string cacheKey = GetCacheKey(args.IsApplicationCache);
                string cacheKey = args.Account.HomeAccountId.Identifier;
                if (!string.IsNullOrWhiteSpace(cacheKey))
                {
                    await WriteCacheBytesAsync(cacheKey, args.TokenCache.SerializeMsalV3()).ConfigureAwait(false);
                }
            }
        }

        private async Task OnBeforeAccessAsync(TokenCacheNotificationArgs args)
        {
            string cacheKey = args?.Account?.HomeAccountId?.Identifier;
            {
                //  string cacheKey = GetCacheKey(args.IsApplicationCache);


                if (!string.IsNullOrEmpty(cacheKey))
                {
                    byte[] tokenCacheBytes = await ReadCacheBytesAsync(cacheKey).ConfigureAwait(false);
                    args.TokenCache.DeserializeMsalV3(tokenCacheBytes, shouldClearExistingCache: true);
                }
            }
        }

        // if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        protected virtual Task OnBeforeWriteAsync(TokenCacheNotificationArgs args)
        {
            return Task.CompletedTask;
        }

        public async Task ClearAsync()
        {
            // This is here a user token cache
            // await RemoveKeyAsync(GetCacheKey(false)).ConfigureAwait(false);
        }

        /// <summary>
        /// Method to be implemented by concrete cache serializers to write the cache bytes
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        /// <param name="bytes">Bytes to write</param>
        /// <returns></returns>
        protected abstract Task WriteCacheBytesAsync(string cacheKey, byte[] bytes);

        /// <summary>
        /// Method to be implemented by concrete cache serializers to Read the cache bytes
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        /// <returns>Read bytes</returns>
        protected abstract Task<byte[]> ReadCacheBytesAsync(string cacheKey);

        /// <summary>
        /// Method to be implemented by concrete cache serializers to remove an entry from the cache
        /// </summary>
        /// <param name="cacheKey">Cache key</param>
        protected abstract Task RemoveKeyAsync(string cacheKey);
    }

    /// <summary>
    /// An implementation of token cache for both Confidential and Public clients backed by MemoryCache.
    /// </summary>
    /// <seealso cref="https://aka.ms/msal-net-token-cache-serialization"/>
    public class MsalDistributedTokenCacheAdapter : MsalAbstractTokenCacheProvider
    {
        private readonly IDataProtector _dataProtector;

        /// <summary>
        /// .NET Core Memory cache
        /// </summary>
        private readonly IDistributedCache _distributedCache;

        /// <summary>
        /// Msal memory token cache options
        /// </summary>
        private readonly DistributedCacheEntryOptions _cacheOptions;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="azureAdOptions"></param>
        /// <param name="httpContextAccessor"></param>
        /// <param name="memoryCache"></param>
        /// <param name="cacheOptions"></param>
        public MsalDistributedTokenCacheAdapter(
                                            IDataProtectionProvider dataProtectorProvider, 
                                            IDistributedCache memoryCache,
                                            IOptions<DistributedCacheEntryOptions> cacheOptions) :
            base()
        {
            _dataProtector = dataProtectorProvider.CreateProtector(nameof(MsalDistributedTokenCacheAdapter));
            _distributedCache = memoryCache;
            _cacheOptions = cacheOptions.Value;
        }

        protected override async Task RemoveKeyAsync(string cacheKey)
        {
            await _distributedCache.RemoveAsync(cacheKey).ConfigureAwait(false);
        }

        protected override async Task<byte[]> ReadCacheBytesAsync(string cacheKey)
        {
            try
            {
                var data = await _distributedCache.GetAsync(cacheKey).ConfigureAwait(false);
               
                if (data == null)
                    return null;

                return _dataProtector.Unprotect(data);
           
            }catch (CryptographicException)
            {
                return null;
            }
        }

        protected override async Task WriteCacheBytesAsync(string cacheKey, byte[] bytes)
        {
            await _distributedCache.SetAsync(cacheKey, _dataProtector.Protect(  bytes), _cacheOptions).ConfigureAwait(false);
        }
    }
    public class MsalAccount : IAccount
    {
        public string Username { get; set; }

        public string Environment { get; set; }

        public AccountId HomeAccountId { get; set; }
        public static MsalAccount FromMsalAccountId(string id)
        {
            var parts = id.Split('.');
            return new MsalAccount
            {
                HomeAccountId = new AccountId(id, parts[0], parts[1])
            };
        }
    }

    //public class DataPlatformConfidentialClientFactory
    //{
    //    public DataPlatformConfidentialClientFactory(IMsalTokenCacheProvider msalTokenCacheProvider)
    //    {
    //        this._tokenCacheProvider = msalTokenCacheProvider ?? throw new ArgumentNullException(nameof(msalTokenCacheProvider));
    //    }

    //    private IConfidentialClientApplication application;
    //    private readonly IMsalTokenCacheProvider _tokenCacheProvider;

    //    //public async Task<AuthenticationResult> AddAccountToCacheFromAuthorizationCodeAsync(HttpContext httpContext, string redirect, string code, string[] scopes)
    //    //{

    //    //    var app = GetOrBuildConfidentialClientApplication(redirect);

    //    //    var token = await app.AcquireTokenByAuthorizationCode(scopes, code).ExecuteAsync();
    //    //    httpContext.StoreTokenUsedToCallWebAPI(new JwtSecurityToken(token.AccessToken));

    //    //    var test = await BuildApiClient().AcquireTokenOnBehalfOf(new[] { $"https://graph.microsoft.com/Sites.Read.All", "offline_access" }, new UserAssertion(token.AccessToken))
    //    //                .ExecuteAsync();

    //    //    return test;


    //    //}

    //    /// <summary>
    //    /// Creates an MSAL Confidential client application if needed
    //    /// </summary>
    //    /// <param name="claimsPrincipal"></param>
    //    /// <returns></returns>
    //    public IConfidentialClientApplication GetOrBuildConfidentialClientApplication(string redirect)
    //    {
    //        if (application == null)
    //        {
    //            application = BuildConfidentialClientApplication(redirect);
    //        }
    //        return application;
    //    }

    //    private IConfidentialClientApplication BuildConfidentialClientApplication(string redirect)
    //    {
    //        var app = ConfidentialClientApplicationBuilder.Create("00488176-ef4c-480f-998e-00a96b4f6c5c")
    //                  .WithClientSecret("[397r2=@BRnZ?tSYDFBTU1PWzNNxBd:S")
    //                .WithRedirectUri(redirect)
    //                .Build();

    //        //var app = ConfidentialClientApplicationBuilder.Create("39e106c5-5ae1-4ff7-9cef-2e61e3be0609")
    //        //          .WithClientSecret("dUrGc02]y?03DfHV]cDEu?_[R@UJ336y")
    //        //         .WithRedirectUri(redirect)
    //        //         .Build();

    //        _tokenCacheProvider?.Initialize(app.AppTokenCache);
    //        _tokenCacheProvider?.Initialize(app.UserTokenCache);

    //        return app;

    //    }

    //    public IConfidentialClientApplication BuildApiClient()
    //    {
    //        //var app = ConfidentialClientApplicationBuilder.Create("00488176-ef4c-480f-998e-00a96b4f6c5c")
    //        //          .WithClientSecret("[397r2=@BRnZ?tSYDFBTU1PWzNNxBd:S")
    //        //        .Build();

    //        var app = ConfidentialClientApplicationBuilder.Create("39e106c5-5ae1-4ff7-9cef-2e61e3be0609")
    //                  .WithClientSecret("dUrGc02]y?03DfHV]cDEu?_[R@UJ336y")
    //                 .Build();

    //        _tokenCacheProvider?.Initialize(app.AppTokenCache);
    //        _tokenCacheProvider?.Initialize(app.UserTokenCache);

    //        return app;

    //    }
    //}

}