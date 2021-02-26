using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class VaultTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var app = new Microsoft.Azure.Services.AppAuthentication.AzureServiceTokenProvider();
            var token = await app.GetAuthenticationResultAsync("https://vault.azure.net");
            return new AccessToken(token.AccessToken, token.ExpiresOn);

        }
    }
}
