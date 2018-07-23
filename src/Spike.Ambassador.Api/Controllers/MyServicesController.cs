namespace Spike.Ambassador.Api.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Helpers;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    public class MyServicesController : Controller
    {
        [HttpGet]
        public async Task<IEnumerable<string>> Get()
        {
            HttpClient httpClient = null;
            try
            {
                httpClient = SpikeAmbassadorHttpClient.GetClient();

                var response = await httpClient.GetAsync("api/values").ConfigureAwait(false);

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

        [HttpGet("{id}")]
        public async Task<IEnumerable<string>> Get(int id)
        {
            HttpClient httpClient = null;
            try
            {
                httpClient = SpikeAmbassadorHttpClient.GetClient();

                var response = await httpClient.GetAsync($"api/values/{id}").ConfigureAwait(false);

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