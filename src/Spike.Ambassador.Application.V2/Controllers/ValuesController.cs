namespace Spike.Ambassador.Application.V2.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Api.Helpers;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "app.v2:value1", "app.v2:value2" };
        }


        [HttpGet("{id}")]
        public async Task<IEnumerable<string>> Get(int id)
        {
            HttpClient httpClient = null;
            try
            {
                httpClient = SpikeAmbassadorHttpClient.GetClient();

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
