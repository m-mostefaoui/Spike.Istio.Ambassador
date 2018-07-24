﻿namespace Spike.Ambassador.Application.V1.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Helpers;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new[] { "app.v1:value1", "app.v1:value2" };
        }

        [HttpGet("{id}")]
        public async Task<IEnumerable<string>> Get(int id)
        {
            HttpClient httpClient = null;
            try
            {
                httpClient = SpikeAmbassadorHttpClient.GetClient();
                if(Request.Headers.TryGetValue("config-version", out var configVersionValues))
                {
                   httpClient.DefaultRequestHeaders.Add("config-version",new[]{ configVersionValues.First()});
                }

                var response = await httpClient.GetAsync("api/configs").ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var lstServicesAsString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var services = JsonConvert.DeserializeObject<IList<string>>(lstServicesAsString).ToList();
                    return services;
                }

                throw new Exception($"{(int)response.StatusCode}-{response.StatusCode}");
            }
            catch (Exception e)
            {
                var absoluteUri = httpClient.BaseAddress.AbsoluteUri;
                return new List<string> { $"absoluteUri: {absoluteUri} error: " + e.Message };
            }
        }
    }
}
