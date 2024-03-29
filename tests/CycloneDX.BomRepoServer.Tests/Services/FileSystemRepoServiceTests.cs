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
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class FileSystemRepoServiceTests
    {
        private async Task<IRepoService> CreateRepoService()
        {
            var mfs = new MockFileSystem();
            var options = new FileSystemRepoOptions
            {
                Directory = "repo"
            };
            var repoService = new FileSystemRepoService(mfs, options);
            await repoService.PostConstructAsync();
            return repoService;
        }
        
        [Theory]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", true)]
        [InlineData("{3e671687-395b-41f5-a30f-a58921a69b79}", true)]
        [InlineData("urn_uuid_3e671687-395b-41f5-a30f-a58921a69b70", false)]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b7", false)]
        [InlineData("abc", false)]
        public void ValidSerialNumberTest(string serialNumber, bool valid)
        {
            Assert.Equal(valid, BomController.ValidSerialNumber(serialNumber));            
        }
        
        [Fact]
        public async Task GetAllBomSerialNumbers_ReturnsAll()
        {
            var service = await CreateRepoService();
            await service.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });
            await service.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });
            await service.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });

            var retrievedSerialNumbers = await service.GetAllBomSerialNumbersAsync().ToListAsync();
            retrievedSerialNumbers.Sort();
            
            Assert.Collection(retrievedSerialNumbers, 
                serialNumber => Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", serialNumber),
                serialNumber => Assert.Equal("urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79", serialNumber),
                serialNumber => Assert.Equal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", serialNumber)
            );
        }
        
        [Fact]
        public async Task RetrieveAll_ReturnsAllVersions()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);
            bom.Version = 3;
            await service.StoreAsync(bom);

            var retrievedBoms = await service.RetrieveAllAsync(bom.SerialNumber).ToListAsync();
            
            Assert.Collection(retrievedBoms, 
                bom => Assert.Equal(1, bom.Version),
                bom => Assert.Equal(2, bom.Version),
                bom => Assert.Equal(3, bom.Version)
            );
        }
        
        [Fact]
        public async Task RetrieveLatest_ReturnsLatestVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);
            bom.Version = 3;
            await service.StoreAsync(bom);

            var retrievedBom = await service.RetrieveAsync(bom.SerialNumber);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Fact]
        public async Task StoreBom_StoresSpecificVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };

            await service.StoreAsync(bom);

            var retrievedBom = await service.RetrieveAsync(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Theory]
        [InlineData(SerializationFormat.Xml)]
        [InlineData(SerializationFormat.Json)]
        [InlineData(SerializationFormat.Protobuf)]
        public async Task StoreOriginalBom_RetrievesOriginalContent(SerializationFormat format)
        {
            var service = await CreateRepoService();
            var bom = new byte[] {32, 64, 128};
            using var originalMS = new System.IO.MemoryStream(bom);

            await service.StoreOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1, originalMS, format, SpecificationVersion.v1_2);

            using var result = await service.RetrieveOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1);
            
            Assert.Equal(format, result.Format);
            Assert.Equal(SpecificationVersion.v1_2, result.SpecificationVersion);

            await using var resultMS = new System.IO.MemoryStream();
            await result.BomStream.CopyToAsync(resultMS);
            Assert.Equal(bom, resultMS.ToArray());
        }
        
        [Fact]
        public async Task StoreClashingBomVersion_ThrowsException()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };

            await service.StoreAsync(bom);

            await Assert.ThrowsAsync<BomAlreadyExistsException>(() => service.StoreAsync(bom));
        }
        
        [Fact]
        public async Task StoreBomWithoutVersion_SetsVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid()
            };

            var returnedBom = await service.StoreAsync(bom);

            var retrievedBom = await service.RetrieveAsync(bom.SerialNumber, 1);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(1, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public async Task StoreBomWithPreviousVersions_IncrementsFromPreviousVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };
            // store previous version
            await service.StoreAsync(bom);

            // store new version without a version number
            bom.Version = null;
            var returnedBom = await service.StoreAsync(bom);

            var retrievedBom = await service.RetrieveAsync(returnedBom.SerialNumber, returnedBom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(3, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public async Task Delete_DeletesSpecificVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);
            
            await service.DeleteAsync(bom.SerialNumber, bom.Version.Value);

            var retrievedBom = await service.RetrieveAsync(bom.SerialNumber, bom.Version.Value);
            Assert.Null(retrievedBom);
            retrievedBom = await service.RetrieveAsync(bom.SerialNumber, 1);
            Assert.Equal(bom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(1, retrievedBom.Version);
        }
        
        [Fact]
        public async Task Delete_DeletesBomsFromAllVersions()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);
            
            await service.DeleteAsync(bom.SerialNumber, 1);

            var bomVersions = await service.GetAllVersionsAsync(bom.SerialNumber).ToListAsync();
            
            Assert.Collection(bomVersions, 
                bomVersion =>
                {
                    Assert.Equal(2, bomVersion);
                }
            );
        }
        
        [Fact]
        public async Task DeleteAll_DeletesAllVersions()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);
            
            await service.DeleteAllAsync(bom.SerialNumber);

            var retrievedBom = await service.RetrieveAsync(bom.SerialNumber, 1);
            Assert.Null(retrievedBom);
            retrievedBom = await service.RetrieveAsync(bom.SerialNumber, 2);
            Assert.Null(retrievedBom);
        }
    }
}
