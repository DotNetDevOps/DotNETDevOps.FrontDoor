using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp.Azure.Blob
{
    public static class SemanticVersionEx
    {
        public static bool Satisfies(this SemanticVersion version, string filter)
        {
            if (filter == "*")
                return true;

            if (filter.StartsWith("~"))
            {
                var filterVersion = SemanticVersion.Parse(filter.Substring(1));

                if (version <= filterVersion)
                    return true;

                return false;


            }

            if (filter.StartsWith("^"))
            {
                var filterVersion = SemanticVersion.Parse(filter.Substring(1));

                if (version.Version.Major > filterVersion.Version.Major)
                    return false;


                return true;


            }

            return true;
        }
    }
    public class LibVersion
    {
        public string Version { get; set; }
    }
    public class CDNHelper
    {
        
        public string url { get; }
        public string lib { get; }

        public CDNHelper(string url, string lib)
        {
            this.url = url;
            this.lib = lib;
            
        }
        private static char[] splits = new[] { '/' };
      

        public async Task<LibVersion> GetAsync(string filter = "*", string prerelease =null)
        {
            var blob = new CloudBlobContainer(new Uri(url));
            if (!await blob.ExistsAsync())
            {
                return null;
            }
            var versions = await blob.ListBlobsSegmentedAsync($"{lib}/", null);

            var sems = versions.Results.OfType<CloudBlobDirectory>()
                .Where(c => SemanticVersion.TryParse(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last(), out SemanticVersion semver) && semver.Satisfies(filter) && (string.IsNullOrEmpty(prerelease) ||string.IsNullOrEmpty(semver.SpecialVersion) || semver.SpecialVersion.StartsWith(prerelease)))                
                .OrderByDescending(c => new SemanticVersion(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last()))
                .ToArray();
            if (!sems.Any())
            {
                return null;
            }

            return new LibVersion { Version = sems.FirstOrDefault().Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last() };

        }
    }
}
