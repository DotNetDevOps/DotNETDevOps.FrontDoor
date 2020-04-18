
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace DotNETDevOps.FrontDoor.AspNetCore
{
    public static class ForwardedExtensions
    {
        public static IServiceCollection WithXForwardedHeaders(this IServiceCollection services)
        {
            return services.AddSingleton<IStartupFilter, UseForwardedHeadersStartupFilter>();
        }
    }

    public static class Md5Extensions
    {
        public static string ToMD5Hash(this string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
