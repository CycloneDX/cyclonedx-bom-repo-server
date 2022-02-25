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
using System.Linq;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;
using DotNet.Testcontainers.Containers.WaitStrategies;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class S3RepoServiceTests : IClassFixture<MinioFixture>, IAsyncLifetime
    {
        private IAmazonS3 _s3Client;
        private readonly MinioFixture _minioFixture;
        private string _bucketName;

        public S3RepoServiceTests(MinioFixture minioFixture)
        {
            this._minioFixture = minioFixture;
        }

        [NeedsDockerForCITheory]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", true)]
        [InlineData("urn:uuid:{3e671687-395b-41f5-a30f-a58921a69b79}", true)]
        [InlineData("urn_uuid_3e671687-395b-41f5-a30f-a58921a69b70", false)]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b7", false)]
        [InlineData("abc", false)]
        public void ValidSerialNumberTest(string serialNumber, bool valid)
        {
            Assert.Equal(valid, BomController.ValidSerialNumber(serialNumber));
        }

        [NeedsDockerForCIFact]
        public async Task GetAllBomSerialNumbers_ReturnsAll()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task RetrieveAll_ReturnsAllVersions()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task RetrieveLatest_ReturnsLatestVersion()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task StoreBom_StoresSpecificVersion()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCITheory]
        [InlineData(Format.Xml)]
        [InlineData(Format.Json)]
        [InlineData(Format.Protobuf)]
        public async Task StoreOriginalBom_RetrievesOriginalContent(Format format)
        {
            var service = new S3RepoService(_s3Client, _bucketName);
            var bom = new byte[] {32, 64, 128};
            using var originalMS = new System.IO.MemoryStream(bom);

            await service.StoreOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1, originalMS, format,
                SpecificationVersion.v1_2);

            using (var result = await service.RetrieveOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1)) {
                Assert.Equal(format, result.Format);
                Assert.Equal(SpecificationVersion.v1_2, result.SpecificationVersion);
                using (var resultMS = new System.IO.MemoryStream()) {
                    await result.BomStream.CopyToAsync(resultMS);
                    Assert.Equal(bom, resultMS.ToArray());
                }
            };
        }

        [NeedsDockerForCIFact]
        public async Task StoreClashingBomVersion_ThrowsException()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };

            await service.StoreAsync(bom);

            await Assert.ThrowsAsync<BomAlreadyExistsException>(async () => await service.StoreAsync(bom));
        }

        [NeedsDockerForCIFact]
        public async Task StoreBomWithoutVersion_SetsVersion()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task StoreBomWithPreviousVersions_IncrementsFromPreviousVersion()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task Delete_DeletesSpecificVersion()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        [NeedsDockerForCIFact]
        public async Task Delete_DeletesBomsFromAllVersions()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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
                bomVersion => { Assert.Equal(2, bomVersion); }
            );
        }

        [NeedsDockerForCIFact]
        public async Task DeleteAll_DeletesAllVersions()
        {
            var service = new S3RepoService(_s3Client, _bucketName);
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

        public async Task InitializeAsync()
        {
            var s3TestContext = await _minioFixture.CreateS3TestContext();
            _s3Client = s3TestContext.AmazonS3Client;
            _bucketName = s3TestContext.bucketName;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            // List and delete all objects
            ListObjectsRequest listRequest = new ListObjectsRequest
            {
                BucketName = _bucketName
            };

            ListObjectsResponse listResponse;
            do
            {
                // Get a list of objects
                listResponse = await _s3Client.ListObjectsAsync(listRequest);
                foreach (S3Object obj in listResponse.S3Objects)
                {
                    // Delete each object
                    await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = obj.Key
                    });
                }

                // Set the marker property
                listRequest.Marker = listResponse.NextMarker;
            } while (listResponse.IsTruncated);

            // Construct DeleteBucket request
            DeleteBucketRequest request = new DeleteBucketRequest
            {
                BucketName = _bucketName
            };

            // Issue call
            await _s3Client.DeleteBucketAsync(request);
        }
    }

    public class MinioFixture : IAsyncLifetime, IDisposable
    {
        protected internal record S3TestContext(IAmazonS3 AmazonS3Client, string bucketName);

        private TestcontainersContainer _testContainer;
        private AmazonS3Client _s3Client;
        private string _bucketName;

        protected internal async Task<S3TestContext> CreateS3TestContext()
        {
            await _s3Client.PutBucketAsync(_bucketName);
            return new (_s3Client, _bucketName);
        }

        public async Task InitializeAsync()
        {
            var testcontainersBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("minio/minio")
                .WithName("minio")
                .WithPortBinding(9000, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
                .WithCommand("server", "--console-address", ":9001", "/data")
                .WithCleanUp(true);

            _testContainer = testcontainersBuilder.Build();
            await _testContainer.StartAsync();
            
            var mappedPort = _testContainer.GetMappedPublicPort(9000);
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials("minioadmin", "minioadmin");
            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"http://localhost:{mappedPort}",
                ForcePathStyle = true // MUST be true to work correctly with MinIO server
                
            };
            AWSConfigsS3.UseSignatureVersion4 = true;
            _s3Client = new AmazonS3Client(awsCredentials, s3Config);
            _bucketName = "bomserver";
        }

        public void Dispose()
        {
            _s3Client?.Dispose();
        }
        
        public async Task DisposeAsync()
        {
            try {
                await _testContainer.StopAsync();
            }
            #pragma warning disable CS0168
            catch (InvalidOperationException invalidOperationException)
            #pragma warning restore CS0168
            {
            }
        }


    }
}