namespace Spike.Istio.Ambassador.ConfigService.V2.Controllers
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    public class ConfigsController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new[] { "v2:config1", "v2:config2", "v2:config3" };
        }
    }
}