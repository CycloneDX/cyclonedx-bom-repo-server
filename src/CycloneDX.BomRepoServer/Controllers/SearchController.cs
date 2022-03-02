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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Models;
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
    public class SearchController : ControllerBase
    {
        private readonly AllowedMethodsOptions _allowedMethods;
        private readonly CacheService _cacheService;

        public SearchController(
            AllowedMethodsOptions allowedMethods,
            CacheService cacheService)
        {
            _allowedMethods = allowedMethods;
            _cacheService = cacheService;
        }

        [HttpGet]
        public ActionResult<IEnumerable<BomIdentifier>> Get([CanBeNull] string group, [CanBeNull] string name, [CanBeNull] string version)
        {
            if (!_allowedMethods.Get) return StatusCode(403);
                
            if (
                group == null
                && name == null
                && version == null
            ) return BadRequest("Search requires group, name or version parameters to be specified.");

            return new ObjectResult(_cacheService.Search(group, name, version));
        }
    }
}
