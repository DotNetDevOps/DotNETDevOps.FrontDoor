using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class KeyVaultProvider
    {
        private readonly VaultTokenCredential tokenCredential;
        private readonly IConfiguration configuration;

        public KeyVaultProvider(VaultTokenCredential tokenCredential , IConfiguration configuration)
        {
            this.tokenCredential = tokenCredential;
            this.configuration = configuration;
        }
        public async Task<string> GetValueAsync(string name)
        {


            var client = new SecretClient(new Uri(configuration.GetValue<string>("VaultBaseURL")), tokenCredential);
            var secret = await client.GetSecretAsync(name);
            return secret.Value.Value;
        }
    }
}
