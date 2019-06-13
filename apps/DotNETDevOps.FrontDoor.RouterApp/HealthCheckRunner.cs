using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DotNETDevOps.FrontDoor.RouterApp
{
    public class HealthCheckRunner : BackgroundService
    {
        private List<HeathCheckItem> data;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<HealthCheckRunner> logger;

        public HealthCheckRunner(IHttpClientFactory httpClientFactory, ILogger<HealthCheckRunner> logger)
        {
            data = new List<HeathCheckItem>();
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public void Enqueue(HeathCheckItem item)
        {
            data.Add(item);
            int ci = data.Count - 1; // child index; start at end
            while (ci > 0)
            {
                int pi = (ci - 1) / 2; // parent index
                if (data[ci].CompareTo(data[pi]) >= 0)
                    break; // child item is larger than (or equal) parent so we're done
                HeathCheckItem tmp = data[ci];
                data[ci] = data[pi];
                data[pi] = tmp;
                ci = pi;
            }
        }

        public HeathCheckItem Dequeue()
        {
            // assumes pq is not empty; up to calling code
            int li = data.Count - 1; // last index (before removal)
            HeathCheckItem frontItem = data[0];   // fetch the front
            data[0] = data[li];
            data.RemoveAt(li);

            --li; // last index (after removal)
            int pi = 0; // parent index. start at front of pq
            while (true)
            {
                int ci = pi * 2 + 1; // left child index of parent
                if (ci > li)
                    break;  // no children so done
                int rc = ci + 1;     // right child
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
                    ci = rc;
                if (data[pi].CompareTo(data[ci]) <= 0)
                    break; // parent is smaller than (or equal to) smallest child so done
                HeathCheckItem tmp = data[pi];
                data[pi] = data[ci];
                data[ci] = tmp; // swap parent and child
                pi = ci;
            }
            return frontItem;
        }

        public HeathCheckItem Peek()
        {
            HeathCheckItem frontItem = data[0];
            return frontItem;
        }

        public void AddHealthCheckTask(HeathCheckItem item)
        {
            this.Enqueue(item);
        }
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(initialCount: 1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {

                if(data.Count > 0)
                {

                    await _syncLock.WaitAsync().ConfigureAwait(false);

                    var item = Dequeue();
                    try
                    {
                        var delay = item.NextRun - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay, stoppingToken);

                        if (!stoppingToken.IsCancellationRequested)
                        {

                            using (var http = httpClientFactory.CreateClient("heathcheck"))
                            {
                                try
                                {
                                    var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, item.Url));

                                    if (!resp.IsSuccessStatusCode)
                                    {
                                        logger.LogWarning("{url} is not live: {status}", item.Url, resp.StatusCode);
                                        item.Failed++;
                                    }
                                    else
                                    {
                                        item.Failed = 0;
                                    }

                                }
                                catch(Exception ex)
                                {
                                    logger.LogError("{url} is not live", item.Url);
                                }

                            }

                        }
                    }
                    finally
                    {
                        item.NextRun = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(item.Interval);
                        Enqueue(item);
                        _syncLock.Release();
                    }
                }


                if (data.Count == 0)
                    await Task.Delay(5000, stoppingToken);



            }
        }

        internal async Task ClearAsync()
        {
            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                this.data.Clear();

            }
            finally
            {
                _syncLock.Release();
            }
        }
    }
}
