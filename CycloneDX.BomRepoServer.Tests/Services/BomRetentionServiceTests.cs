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
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class BomRetentionServiceTests
    {
        [Fact]
        public void Retention_Removes_ExtraBomVersions()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var repoService = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "Test"
                    }
                }
            };
            repoService.Store(bom);
            bom.Version = 2;
            repoService.Store(bom);
            bom.Version = 3;
            repoService.Store(bom);
            var service = new BomRetentionService(new RetentionOptions { MaxBomVersions = 2 }, repoService);
            
            service.ProcessRetention();

            var bomVersions = repoService.GetAllVersions(bom.SerialNumber);
            
            Assert.Collection(bomVersions, 
                bomVersion =>
                {
                    Assert.Equal(2, bomVersion);
                },
                bomVersion =>
                {
                    Assert.Equal(3, bomVersion);
                }
            );
        }
    }
}
