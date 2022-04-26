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
using CycloneDX.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Services
{
    public class CacheService
    {
        // Internal utility class to minimise memory usage to elements that are actually used for searching
        private class BomSubset
        {
            public string SerialNumber { get; }
            public int Version { get; }
            public string ComponentGroup { get; }
            public string ComponentName { get; }
            public string ComponentVersion { get; }

            public BomSubset(Bom bom)
            {
                SerialNumber = bom.SerialNumber;
                Version = bom.Version.Value;
                if (bom.Metadata != null)
                {
                    if (bom.Metadata.Component != null)
                    {
                        ComponentGroup = bom.Metadata.Component.Group?.ToLowerInvariant();
                        ComponentName = bom.Metadata.Component.Name?.ToLowerInvariant();
                        ComponentVersion = bom.Metadata.Component.Version?.ToLowerInvariant();
                    }
                }
            }
        }

        private readonly IRepoService _repoService;
        private readonly ILogger _logger;

        private readonly Dictionary<ValueTuple<string, int>, BomSubset> _bomCache = new ();

        public CacheService(IRepoService repoService, ILogger logger = null)
        {
            _repoService = repoService;
            _logger = logger;
        }

        public async Task UpdateCache()
        {
            var existingEntries = _bomCache.Keys.ToList();

            await foreach (var serialNumber in _repoService.GetAllBomSerialNumbersAsync())
            {
                await foreach (var bom in _repoService.RetrieveAllAsync(serialNumber))
                {
                    existingEntries.Remove((serialNumber, bom.Version.Value));
                    // add new/update existing BOMs
                    Add(serialNumber, bom.Version.Value, bom);
                }
            }

            // remove BOMs that no longer exist
            foreach (var oldEntry in existingEntries) Remove(oldEntry.Item1, oldEntry.Item2);
        }

        public IEnumerable<BomIdentifier> Search(string group = null, string name = null, string version = null)
        {
            IEnumerable<BomSubset> results = _bomCache.Values;
            if (!string.IsNullOrEmpty(name))
            {
                results = results.Where(bom => bom.ComponentName == name.ToLowerInvariant());
            }
            if (!string.IsNullOrEmpty(group))
            {
                results = results.Where(bom => bom.ComponentGroup == group.ToLowerInvariant());
            }
            if (!string.IsNullOrEmpty(version))
            {
                results = results.Where(bom => bom.ComponentVersion == version.ToLowerInvariant());
            }

            foreach (var result in results)
            {
                yield return new BomIdentifier(result.SerialNumber, result.Version);
            }
        }

        private void Add(string serialNumber, int version, Bom bom)
        {
            _bomCache[(serialNumber, version)] = new BomSubset(bom);
        }

        private void Remove(string serialNumber, int version)
        {
            _bomCache.Remove((serialNumber, version));
        }
    }
}