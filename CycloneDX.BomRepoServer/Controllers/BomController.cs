// This file is part of CycloneDX BOM Repository Server
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) Patrick Dwyer. All Rights Reserved.
    
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
        public ActionResult<CycloneDX.Models.v1_3.Bom> Get(string serialNumber, int? version)
        {
            if (!_allowedMethods.Get) return StatusCode(403);
            if (!ValidSerialNumber(serialNumber)) return BadRequest("Invalid serialNumber provided");
                
            if (serialNumber == null) return BadRequest("serialNumber is a required parameter");

            CycloneDX.Models.v1_3.Bom result;
            if (version.HasValue)
                result = _repoService.Retrieve(serialNumber, version.Value);
            else
                result = _repoService.RetrieveLatest(serialNumber);

            if (result == null) return NotFound();
            
            return result;
        }

        [HttpPost]
        public ActionResult Post(CycloneDX.Models.v1_3.Bom bom)
        {
            if (!_allowedMethods.Post) return StatusCode(403);

            if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();
            if (!ValidSerialNumber(bom.SerialNumber)) return BadRequest("Invalid BOM SerialNumber provided");
            
            try
            {
                var result = _repoService.Store(bom);
                var routeValues = new {serialNumber = result.SerialNumber, version = result.Version};
                return CreatedAtAction(nameof(Get), routeValues, "");
            }
            catch (BomAlreadyExistsException)
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
