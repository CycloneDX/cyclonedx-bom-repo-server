using System;
using System.IO;
using CycloneDX.BomRepo.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BomController : ControllerBase
    {
        private readonly RepoOptions _repoOptions;
        private readonly ILogger<BomController> _logger;

        public BomController(RepoOptions repoOptions, ILogger<BomController> logger)
        {
            _repoOptions = repoOptions;
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<Models.v1_3.Bom> Get(string serialNumber)
        {
            if (serialNumber == null) return BadRequest("id is a required parameter");
            
            var filePath = Path.Combine(_repoOptions.Directory, serialNumber.Replace(':', '_'));

            if (System.IO.File.Exists(filePath))
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var bom = Protobuf.Deserializer.Deserialize(fs);
                    return bom;
                }
            }

            return NotFound();
        }
    }
}
