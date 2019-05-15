using System;
using Xunit;

namespace DotNETDevOps.FrontDoor.UnitTests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var urlString = "http://dotnetdevops-cdn/libs/frontdoor--front/1.0.0-ci-20190515.07/index.html";
            var url = new Uri(urlString);
            
            Assert.Equal("dotnetdevops-cdn", url.Host);
        }
    }
}
