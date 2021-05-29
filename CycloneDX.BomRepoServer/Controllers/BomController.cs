using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BomController : ControllerBase
    {
        private static readonly Regex SerialNumberRegex = new Regex(
            @"^urn:uuid:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})|(\{[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\})$");

        private readonly AllowedMethodsOptions _allowedMethods;
        private readonly RepoService _repoService;
        private readonly ILogger<BomController> _logger;

        public BomController(AllowedMethodsOptions allowedMethods, RepoService repoService, ILogger<BomController> logger)
        {
            _allowedMethods = allowedMethods;
            _repoService = repoService;
            _logger = logger;
        }

        internal static bool ValidSerialNumber(string serialNumber)
        {
            return SerialNumberRegex.IsMatch(serialNumber);
        }
        
        [HttpGet]
        public async Task<ActionResult<Models.v1_3.Bom>> Get(string serialNumber, int? version)
        {
            if (!_allowedMethods.Get) return StatusCode(403);
            if (!ValidSerialNumber(serialNumber)) return BadRequest("Invalid serialNumber provided");
                
            if (serialNumber == null) return BadRequest("serialNumber is a required parameter");

            Models.v1_3.Bom result;
            if (version.HasValue)
                result = await _repoService.Retrieve(serialNumber, version.Value);
            else
                result = await _repoService.RetrieveLatest(serialNumber);

            if (result == null) return NotFound();
            
            return result;
        }

        [HttpPost]
        public async Task<ActionResult> Post(Models.v1_3.Bom bom)
        {
            if (!_allowedMethods.Post) return StatusCode(403);

            if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();
            if (!ValidSerialNumber(bom.SerialNumber)) return BadRequest("Invalid BOM SerialNumber provided");
            
            try
            {
                var result = await _repoService.Store(bom);
                var routeValues = new {serialNumber = result.SerialNumber, version = result.Version};
                return CreatedAtAction(nameof(Get), routeValues, "");
            }
            catch (BomAlreadyExistsException e)
            {
                return Conflict($"BOM with serial number {bom.SerialNumber} and version {bom.Version} already exists.");
            }
        }
        
        [HttpDelete]
        public ActionResult Delete(string serialNumber, int? version)
        {
            if (!_allowedMethods.Delete) return StatusCode(403);
            if (!ValidSerialNumber(serialNumber)) return BadRequest("Invalid serialNumber provided");

            if (serialNumber == null) return BadRequest("serialNumber is a required parameter");

            if (version.HasValue)
                _repoService.Delete(serialNumber, version.Value);
            else
                _repoService.DeleteAll(serialNumber);

            return Ok();
        }
    }
}
