using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using DotNETDevOps.FrontDoor.AspNetCore;
using Newtonsoft.Json;

namespace DotNETDevOps.FrontDoor.UnitTests
{
    public class TopLevelProvider
    {

    }

    public class Node
    {
        public string Id { get; set; }
        public string Display { get; set; }
    }
    public class DomainNode : Node
    {
        public DomainNode(string domain)
        {
           Display= Id = Domain = domain;
        }
        public string Domain { get; set; }
        public DomainNode[] SubDomains { get; set; }
    }
    
    public class LocationNode : Node
    {
        public LocationNode(string hash , string display)
        {
            Id = hash;
            Display = display;
        }
    }
    public class Edge : Node
    {
        public Edge(string source, string target)
        {
            Source = source;
            Target = target;
            Id=(source+target).ToMD5Hash();
        }
        
        public string Source { get; set; }
        public string Target { get; set; }
        
    }
    public class LocationHostEdge : Edge
    {
        public LocationHostEdge(string source, string target) : base(source, target)
        {
        }
    }

    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var urlString = "http://dotnetdevops-cdn/libs/frontdoor--front/1.0.0-ci-20190515.07/index.html";
            var url = new Uri(urlString);

            Assert.Equal("dotnetdevops-cdn", url.Host);
        }

        [Fact]
        public async Task Test2()
        {
            var path = @"C:\dev\DotNETDevOps.FrontDoor\apps\DotNETDevOps.FrontDoor.RouterApp\routes.json";
            var jtoken = JToken.Parse(File.ReadAllText(path));
            var http = new HttpClient();
            var tld = await http.GetStreamAsync("https://publicsuffix.org/list/public_suffix_list.dat");

            var hash = new HashSet<string>();

            using (var sr = new StreamReader(tld))
            {

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    if (!line.StartsWith("//"))
                    {
                        hash.Add(line);
                    }

                }

            }

            var nodes = new List<Node>();
            var hostNames = jtoken.SelectTokens("$.servers[*].hostnames[*]").Select(k => k.ToString()).ToArray().GroupBy(h => FindTopLevel(h, hash));

            var toplevlHosts = hostNames.Select(k => new DomainNode(k.Key) { SubDomains = k.Where(d=>d!=k.Key).Select(s => new DomainNode(s)).ToArray() }).ToArray();

            var locations = jtoken.SelectTokens("$.servers[*].locations[*]").ToLookup(k => k.ToString().ToMD5Hash());


            nodes.AddRange(locations.Select(l => new LocationNode(l.Key,l.First().SelectToken("$.proxy_pass").ToString())));

            foreach(var server in jtoken.SelectToken("$.servers"))
            {
                foreach(var hostname in server.SelectToken("$.hostnames"))
                {
                    foreach(var location in server.SelectToken("$.locations"))
                    {
                        
                        nodes.Add(new LocationHostEdge( hostname.ToString(), location.ToString().ToMD5Hash()));
                    }
                }
            }

            foreach(var node in toplevlHosts)
            {
                nodes.Add(node);
                nodes.AddRange(node.SubDomains);
               // nodes.AddRange(node.SubDomains.Select(k=>new Edge(node.Id,k.Id) ));
            }


            var str = JsonConvert.SerializeObject(nodes.Select(k => new { data = k }),new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() });
        




        }


        private string FindTopLevel(string h, HashSet<string> hash)
        {
            var domain = "";
            while (h.Contains('.') && !hash.Contains(h))
            {
                domain = h.Substring(0, h.IndexOf('.'));
                h = h.Substring(h.IndexOf('.')+1);
            }
            if (string.IsNullOrEmpty(domain))
                return h;

            return string.Join(".", domain, h);
        }
    }
}
