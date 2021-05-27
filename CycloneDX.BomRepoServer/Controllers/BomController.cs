using System;
using System.IO;
using CycloneDX.BomRepoServer.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Controllers
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
            if (serialNumber == null) return BadRequest("serialNumber is a required parameter");
            
            var fileName = Path.Combine(_repoOptions.Directory, serialNumber.Replace(':', '_'));

            if (System.IO.File.Exists(fileName))
            {
                using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    var bom = Protobuf.Deserializer.Deserialize(fs);
                    return bom;
                }
            }

            return NotFound();
        }

        [HttpPost]
        public ActionResult Post(Models.v1_3.Bom bom)
        {
            if (string.IsNullOrEmpty(bom.SerialNumber))
            {
                bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();
            }
            
            var fileName = Path.Combine(_repoOptions.Directory, bom.SerialNumber.Replace(':', '_'));

            if (System.IO.File.Exists(fileName))
            {
                return Conflict($"BOM with serial number {bom.SerialNumber} already exists.");
            }
            else
            {
                using var fs = System.IO.File.Open(fileName, FileMode.CreateNew, FileAccess.Write);
                Protobuf.Serializer.Serialize(fs, bom);
                return CreatedAtRoute("bom", bom.SerialNumber, null);
            }
        }
        
        [HttpDelete]
        public ActionResult Delete(string serialNumber)
        {
            if (serialNumber == null) return BadRequest("serialNumber is a required parameter");

            var fileName = Path.Combine(_repoOptions.Directory, serialNumber.Replace(':', '_'));

            System.IO.File.Delete(fileName);

            return Ok();
        }
    }
}
