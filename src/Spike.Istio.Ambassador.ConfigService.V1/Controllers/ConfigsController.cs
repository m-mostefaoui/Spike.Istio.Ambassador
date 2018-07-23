namespace Spike.Istio.Ambassador.ConfigService.V1.Controllers
{
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    public class ConfigsController : Controller
    {
        // GET api/values
        [HttpGet]
        public IEnumerable<string> GetAllConfigs()
        {
            return new[] {"v1:config1", "v1:config2", "v1:config3"};
        }
    }
}