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
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class AWSRepoServiceTests
    {
        [Theory]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", true)]
        [InlineData("urn:uuid:{3e671687-395b-41f5-a30f-a58921a69b79}", true)]
        [InlineData("urn_uuid_3e671687-395b-41f5-a30f-a58921a69b70", false)]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b7", false)]
        [InlineData("abc", false)]
        public void ValidSerialNumberTest(string serialNumber, bool valid)
        {
            Assert.Equal(valid, BomController.ValidSerialNumber(serialNumber));            
        }
        
        [Fact]
        public void GetAllBomSerialNumbers_ReturnsAll()
        {
            var service = new S3RepoService(null);
            service.Store(new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });
            service.Store(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });
            service.Store(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
            });

            var retrievedSerialNumbers = service.GetAllBomSerialNumbers().ToList();
            retrievedSerialNumbers.Sort();
            
            Assert.Collection(retrievedSerialNumbers, 
                serialNumber => Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", serialNumber),
                serialNumber => Assert.Equal("urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79", serialNumber),
                serialNumber => Assert.Equal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", serialNumber)
            );
        }
        
        [Fact]
        public void RetrieveAll_ReturnsAllVersions()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            service.Store(bom);
            bom.Version = 2;
            service.Store(bom);
            bom.Version = 3;
            service.Store(bom);

            var retrievedBoms = service.RetrieveAll(bom.SerialNumber);
            
            Assert.Collection(retrievedBoms, 
                bom => Assert.Equal(1, bom.Version),
                bom => Assert.Equal(2, bom.Version),
                bom => Assert.Equal(3, bom.Version)
            );
        }
        
        [Fact]
        public void RetrieveLatest_ReturnsLatestVersion()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            service.Store(bom);
            bom.Version = 2;
            service.Store(bom);
            bom.Version = 3;
            service.Store(bom);

            var retrievedBom = service.Retrieve(bom.SerialNumber);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Fact]
        public void StoreBom_StoresSpecificVersion()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };

            service.Store(bom);

            var retrievedBom = service.Retrieve(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Theory]
        [InlineData(Format.Xml)]
        [InlineData(Format.Json)]
        [InlineData(Format.Protobuf)]
        public async Task StoreOriginalBom_RetrievesOriginalContent(Format format)
        {
            var service = new S3RepoService(null);
            var bom = new byte[] {32, 64, 128};
            using var originalMS = new System.IO.MemoryStream(bom);

            await service.StoreOriginal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1, originalMS, format, SpecificationVersion.v1_2);

            using var result = service.RetrieveOriginal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1);
            
            Assert.Equal(format, result.Format);
            Assert.Equal(SpecificationVersion.v1_2, result.SpecificationVersion);

            using var resultMS = new System.IO.MemoryStream();
            await result.BomStream.CopyToAsync(resultMS);
            Assert.Equal(bom, resultMS.ToArray());
        }
        
        [Fact]
        public void StoreClashingBomVersion_ThrowsException()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };

            service.Store(bom);

            Assert.Throws<BomAlreadyExistsException>(() => service.Store(bom));
        }
        
        [Fact]
        public void StoreBomWithoutVersion_SetsVersion()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid()
            };

            var returnedBom = service.Store(bom);

            var retrievedBom = service.Retrieve(bom.SerialNumber, 1);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(1, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public void StoreBomWithPreviousVersions_IncrementsFromPreviousVersion()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };
            // store previous version
            service.Store(bom);

            // store new version without a version number
            bom.Version = null;
            var returnedBom = service.Store(bom);

            var retrievedBom = service.Retrieve(returnedBom.SerialNumber, returnedBom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(3, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public void Delete_DeletesSpecificVersion()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            service.Store(bom);
            bom.Version = 2;
            service.Store(bom);
            
            service.Delete(bom.SerialNumber, bom.Version.Value);

            var retrievedBom = service.Retrieve(bom.SerialNumber, bom.Version.Value);
            Assert.Null(retrievedBom);
            retrievedBom = service.Retrieve(bom.SerialNumber, 1);
            Assert.Equal(bom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(1, retrievedBom.Version);
        }
        
        [Fact]
        public void Delete_DeletesBomsFromAllVersions()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            service.Store(bom);
            bom.Version = 2;
            service.Store(bom);
            
            service.Delete(bom.SerialNumber, 1);

            var bomVersions = service.GetAllVersions(bom.SerialNumber);
            
            Assert.Collection(bomVersions, 
                bomVersion =>
                {
                    Assert.Equal(2, bomVersion);
                }
            );
        }
        
        [Fact]
        public void DeleteAll_DeletesAllVersions()
        {
            var service = new S3RepoService(null);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            service.Store(bom);
            bom.Version = 2;
            service.Store(bom);
            
            service.DeleteAll(bom.SerialNumber);

            var retrievedBom = service.Retrieve(bom.SerialNumber, 1);
            Assert.Null(retrievedBom);
            retrievedBom = service.Retrieve(bom.SerialNumber, 2);
            Assert.Null(retrievedBom);
        }
    }
}
