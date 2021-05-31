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
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using Microsoft.Extensions.Logging;

namespace CycloneDX.BomRepoServer.Services
{
    public class MetadataService
    {
        private readonly RepoService _repoService;
        private readonly ILogger _logger;

        public MetadataService(RepoService repoService, ILogger logger = null)
        {
            _repoService = repoService;
            _logger = logger;
        }
    }
}