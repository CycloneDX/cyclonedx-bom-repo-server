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
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Models;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.Models.v1_3;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Services
{
    public class RetentionService
    {
        private readonly RetentionOptions _options;
        private readonly FileSystemRepoService _repoService;
        private readonly ILogger _logger;

        public RetentionService(RetentionOptions options, FileSystemRepoService repoService, ILogger logger = null)
        {
            _options = options;
            _repoService = repoService;
            _logger = logger;
        }

        public void ProcessRetention()
        {
            foreach (var serialNumber in _repoService.GetAllBomSerialNumbers())
            {
                ProcessMaxBomVersionsRetention(serialNumber);
                ProcessMaxBomAgeRetention(serialNumber);
            }
        }

        private void ProcessMaxBomVersionsRetention(string serialNumber)
        {
            if (_options.MaxBomVersions > 0)
            {
                var versions = _repoService.GetAllVersions(serialNumber).ToList();
                while (versions.Count > _options.MaxBomVersions)
                {
                    _repoService.Delete(serialNumber, versions[0]);
                    versions.RemoveAt(0);
                }
            }
        }

        private void ProcessMaxBomAgeRetention(string serialNumber)
        {
            if (_options.MaxBomAge > 0)
            {
                var ageCutOff = DateTime.UtcNow - TimeSpan.FromDays(_options.MaxBomAge);
                
                foreach (var version in _repoService.GetAllVersions(serialNumber))
                {
                    var bomAge = _repoService.GetBomAge(serialNumber, version);
                    if (bomAge < ageCutOff)
                    {
                        _repoService.Delete(serialNumber, version);
                    }
                }
            }
        }
    }
}