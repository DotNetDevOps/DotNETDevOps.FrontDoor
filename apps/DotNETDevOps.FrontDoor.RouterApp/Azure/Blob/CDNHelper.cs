using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp.Blob
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
    public class CDNHelperFactory
    {
        private MemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = 12
        });

        
        public CDNHelper CreateCDNHelper(string url, string lib)
        {
            var key = url + lib;
            CDNHelper cacheEntry;
            if (!_cache.TryGetValue(key, out cacheEntry))// Look for cache key.
            {
                // Key not in cache, so get data.
                cacheEntry = new CDNHelper(url, lib);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                 .SetSize(1)//Size amount
                            //Priority on removing when reaching size limit (memory pressure)
                    .SetPriority(CacheItemPriority.High)
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                    // Remove from cache after this time, regardless of sliding expiration
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(60*5));

                // Save data in cache.
                _cache.Set(key, cacheEntry, cacheEntryOptions);
            }
            return cacheEntry;

          
        }
    }
    public class CDNHelper
    {

        private MemoryCache _cache = new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = 12
        });

        public string url { get; }
        public string lib { get; }

        public CDNHelper(string url, string lib)
        {
            this.url = url;
            this.lib = lib;
            
        }
        private static char[] splits = new[] { '/' };


       

        public  Task<LibVersion> GetAsync(string filter = "*", string prerelease =null)
        {
            var key = filter + prerelease;
            

            return _cache.GetOrCreateAsync(key, entry=> Factory(entry,filter,prerelease));

            

        }

        private async Task<LibVersion> Factory(ICacheEntry arg,string filter, string prerelease)
        {

            arg.SetSize(1);
            arg.SetPriority(CacheItemPriority.High)
                  // Keep in cache for this time, reset time if accessed.
                  .SetSlidingExpiration(TimeSpan.FromSeconds(60))
                  // Remove from cache after this time, regardless of sliding expiration
                  .SetAbsoluteExpiration(TimeSpan.FromSeconds(60 * 5));
            
            var blob = new BlobContainerClient(new Uri(url));
             
            
            //  var blob = new CloudBlobContainer(new Uri(url));
            if (!await blob.ExistsAsync())
            {
                return null;
            }
            var versions = blob.GetBlobsByHierarchyAsync( Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None,"/",$"{lib}/");
         
             await foreach(var result in versions
                .Where(c=>c.IsPrefix)
                .Where(c => SemanticVersion.TryParse(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last(), out SemanticVersion semver) && semver.Satisfies(filter) && (string.IsNullOrEmpty(prerelease) || string.IsNullOrEmpty(semver.SpecialVersion) || semver.SpecialVersion.StartsWith(prerelease)))
                .OrderByDescending(c => new SemanticVersion(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last()))
                )
            {
                return new LibVersion { Version = result.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last() };
            }

            return null;

            //var sems = versions.Results.OfType<CloudBlobDirectory>()
            //    .Where(c => SemanticVersion.TryParse(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last(), out SemanticVersion semver) && semver.Satisfies(filter) && (string.IsNullOrEmpty(prerelease) || string.IsNullOrEmpty(semver.SpecialVersion) || semver.SpecialVersion.StartsWith(prerelease)))
            //    .OrderByDescending(c => new SemanticVersion(c.Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last()))
            //    .ToArray();
            //if (!sems.Any())
            //{
            //    return null;
            //}

                               
          

            //return new LibVersion { Version = sems.FirstOrDefault().Prefix.Split(splits, StringSplitOptions.RemoveEmptyEntries).Last() };
        }
    }
}
