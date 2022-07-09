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
// Copyright (c) OWASP Foundation. All Rights Reserved.
    
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepoServer.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class BomExchangeController : ControllerBase
    {
        //TODO - these regexes and parsing methods need to be implemented in the CDX .NET library
        private static readonly Regex SerialNumberRegex = new Regex(
            @"^(urn:uuid:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})|(\{[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\}))$");

        private static readonly Regex CdxUrnRegex = new Regex(
            @"^urn:cdx:(?<serialNumber>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\/(?<version>[1-9]\d*)$");

        private readonly AllowedMethodsOptions _allowedMethods;
        private readonly IRepoService _repoService;
        private readonly ILogger<BomController> _logger;

        public BomExchangeController(AllowedMethodsOptions allowedMethods, IRepoService repoService, ILogger<BomController> logger = null)
        {
            _allowedMethods = allowedMethods;
            _repoService = repoService;
            _logger = logger;
        }

        internal static bool ValidSerialNumber(string serialNumber)
        {
            return SerialNumberRegex.IsMatch(serialNumber);
        }
        
        internal static bool ValidCdxUrn(string cdxUrn)
        {
            return CdxUrnRegex.IsMatch(cdxUrn);
        }

        internal static Tuple<string,int> ParseCdxUrn(string cdxUrn)
        {
            var match = CdxUrnRegex.Match(cdxUrn);
            var result = new Tuple<string,int>(match.Groups["serialNumber"].Value, int.Parse(match.Groups["version"].Value));
            return result;
        }
        
        /// <summary>Get BOM by sepcify valid serial number(urn:uuid) or CDX URN(urn:cdx)</summary>
        /// <param name="bomIdentifier">Required: serial number(urn:uuid) or CDX URN(urn:cdx)</param>
        /// <returns>Matching BOM content</returns>
        /// <response code="200">Returns matching BOM</response>
        /// <response code="400">Invalid bomIdentifier</response>
        /// <response code="403">If no matching BOM found</response>
        [HttpGet("{bomIdentifier}")]
        public async Task<ActionResult<CycloneDX.Models.Bom>> Get(string bomIdentifier)
        {
            if (!_allowedMethods.Get) return StatusCode(403);

            if (ValidCdxUrn(bomIdentifier))
            {
                var parsedCdxUrn = ParseCdxUrn(bomIdentifier);
                var result = await _repoService.RetrieveAsync($"urn:uuid:{parsedCdxUrn.Item1}", parsedCdxUrn.Item2);
                return result == null ? NotFound() : result;
            }
            else if (ValidSerialNumber(bomIdentifier))
            {
                var result = await _repoService.RetrieveAsync(bomIdentifier, null);
                return result == null ? NotFound() : result;
            }
            else
            {
                return BadRequest("Invalid BOM identifier provided. It must be a BOM serial number UUID URN or CDX URN");
            }
        }
        
        /// <summary>
        /// Add new BOM by request body and correct header
        /// </summary>
        /// TODO: add more document
        [HttpPost]
        public async Task<ActionResult> Post()
        {
            if (!_allowedMethods.Post) return StatusCode(403);

            var contentType = new ContentType(Request.Headers["Content-Type"].ToString());
            var specificationVersion = SpecificationVersion.v1_3;

            if (contentType.Parameters?.ContainsKey("version") == true)
            {
                if (!Enum.TryParse<SpecificationVersion>("v" + contentType.Parameters["version"].Replace('.', '_'),
                    out specificationVersion))
                {
                    return BadRequest(
                        $"Unable to parse schema version in Content-Type header: {Request.Headers["Content-Type"]}");
                }
            }

            var originalBomStream = new MemoryStream();
            CycloneDX.Models.Bom bom;
            SerializationFormat format;
            
            if (contentType.MediaType == MediaTypes.Xml
                || contentType.MediaType == "text/xml"
                || contentType.MediaType == "application/xml"
            )
            {
                format = SerializationFormat.Xml;
                await Request.Body.CopyToAsync(originalBomStream);
                originalBomStream.Position = 0;
                bom = Xml.Serializer.Deserialize(originalBomStream);
            }
            else if (contentType.MediaType == MediaTypes.Json
                || contentType.MediaType == "application/json"
            )
            {
                format = SerializationFormat.Json;
                await Request.Body.CopyToAsync(originalBomStream);
                originalBomStream.Position = 0;
                bom = Json.Serializer.Deserialize(Encoding.UTF8.GetString(originalBomStream.ToArray()));
            }
            else if (contentType.MediaType == MediaTypes.Protobuf
                || contentType.MediaType == "application/octet-stream"
            )
            {
                format = SerializationFormat.Protobuf;
                await Request.Body.CopyToAsync(originalBomStream);
                originalBomStream.Position = 0;
                bom = Protobuf.Serializer.Deserialize(originalBomStream);
            }
            else
            {
                return new ContentResult
                {
                    Content = $"Unacceptable content media types requested. Some valid options are {MediaTypes.Xml}, {MediaTypes.Json} and {MediaTypes.Protobuf}",
                    ContentType = "text/plain",
                    StatusCode = 406
                };
            }

            if (bom == null) return BadRequest();
            
            if (string.IsNullOrEmpty(bom.SerialNumber)) bom.SerialNumber = "urn:uuid:" + Guid.NewGuid();
            if (!ValidSerialNumber(bom.SerialNumber)) return BadRequest("Invalid BOM SerialNumber provided");
            
            try
            {
                originalBomStream.Position = 0;
                var result = await _repoService.StoreAsync(bom);
                await _repoService.StoreOriginalAsync(bom.SerialNumber, bom.Version.Value, originalBomStream, format, specificationVersion);
                var routeValues = new {serialNumber = result.SerialNumber, version = result.Version};
                return CreatedAtAction(nameof(Get), routeValues, "");
            }
            catch (BomAlreadyExistsException)
            {
                return Conflict($"BOM with serial number {bom.SerialNumber} and version {bom.Version} already exists.");
            }
        }
    }
}
