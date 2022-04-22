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
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;
using Amazon.S3;
using Amazon.S3.Model;
using CycloneDX.Xml;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;
using DotNet.Testcontainers.Containers.WaitStrategies;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class S3RepoServiceTests : IClassFixture<MinioFixture>, IAsyncLifetime
    {
        private readonly MinioFixture _minioFixture;
        private string _bucketName;
        private IAmazonS3 _s3Client;

        public S3RepoServiceTests(MinioFixture minioFixture)
        {
            this._minioFixture = minioFixture;
        }

        public Task InitializeAsync()
        {
            _s3Client = _minioFixture.S3Client;
            _bucketName = _minioFixture.BucketName;
            return Task.CompletedTask;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            try
            {
                await _s3Client.Paginators.ListObjectsV2(new ListObjectsV2Request
                    {
                        BucketName = _bucketName
                    })
                    .S3Objects
                    .Select(s3Object => new KeyVersion {Key = s3Object.Key})
                    .ToObservable()
                    .Buffer(999)
                    .Select(list =>
                    {
                        return Observable.FromAsync(async () =>
                            await _s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                            {
                                BucketName = _bucketName,
                                Objects = list.ToList()
                            }));
                    })
                    .Concat()
                    .ToTask();

                await _s3Client.DeleteBucketAsync(new DeleteBucketRequest
                {
                    BucketName = _bucketName
                });
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (!amazonS3Exception.Message.Equals("The specified bucket does not exist"))
                {
                    throw;
                }
            }
        }

        private async Task<IRepoService> CreateRepoService()
        {
            var repoService = new S3RepoService(_s3Client, _bucketName);
            await repoService.PostConstructAsync();
            return repoService;
        }

        [NeedsDockerForCITheory]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", true)]
        [InlineData("{3e671687-395b-41f5-a30f-a58921a69b79}", true)]
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

        [NeedsDockerForCIFact]
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

        [NeedsDockerForCIFact]
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

        [NeedsDockerForCIFact]
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

        [NeedsDockerForCITheory]
        [InlineData(Format.Xml)]
        [InlineData(Format.Json)]
        [InlineData(Format.Protobuf)]
        public async Task StoreOriginalBom_RetrievesOriginalContent(Format format)
        {
            var service = await CreateRepoService();
            var bom = new byte[] {32, 64, 128};
            using var originalMS = new System.IO.MemoryStream(bom);

            await service.StoreOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1, originalMS, format,
                SpecificationVersion.v1_2);

            using var result = await service.RetrieveOriginalAsync("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", 1);
            Assert.Equal(format, result.Format);
            Assert.Equal(SpecificationVersion.v1_2, result.SpecificationVersion);
            await using var memoryStream = new MemoryStream();
            await result.BomStream.CopyToAsync(memoryStream);
            Assert.Equal(bom, memoryStream.ToArray());
        }

        [NeedsDockerForCIFact]
        public async Task StoreClashingBomVersion_ThrowsException()
        {
            var service = await CreateRepoService();
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

        [NeedsDockerForCIFact]
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

            var retrievedBom = await service.RetrieveAsync(returnedBom.SerialNumber, returnedBom.Version!.Value);

            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(3, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }

        [NeedsDockerForCIFact]
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

        [NeedsDockerForCIFact]
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

            Assert.Collection(bomVersions, bomVersion => { Assert.Equal(2, bomVersion); }
            );
        }

        [NeedsDockerForCIFact]
        public async Task RetrieveAllAsync_EmptyDoesNotError()
        {
            var service = await CreateRepoService();

            var bomVersions = await service.RetrieveAllAsync("no-boms").ToListAsync();

            Assert.Empty(bomVersions);
        }

        [NeedsDockerForCIFact]
        public async Task StoreOriginalAsync_DoesNotOverwriteExistingVersion()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
                SpecVersion = "1.3"
            };
            var memoryStream = new MemoryStream();
            Serializer.Serialize(bom, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Null(await Record.ExceptionAsync(async () => await service.StoreOriginalAsync(bom.SerialNumber, bom.Version.Value, memoryStream, Format.Xml, SpecificationVersion.v1_3)));
            await Assert.ThrowsAsync<BomAlreadyExistsException>(async () => await service.StoreOriginalAsync(bom.SerialNumber, bom.Version.Value, memoryStream, Format.Xml, SpecificationVersion.v1_3));
        }

        [NeedsDockerForCIFact]
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

        [NeedsDockerForCIFact]
        public async Task PostConstructAsync_CreatesBucket()
        {
            var exception = await Record.ExceptionAsync(async () => await CreateRepoService());
            Assert.Null(exception);
        }

        [NeedsDockerForCIFact]
        public async Task PostConstructAsync_DoesNotErrorWithExistingBucket()
        {
            await _s3Client.PutBucketAsync(_bucketName);
            var exception = await Record.ExceptionAsync(async () => await CreateRepoService());
            Assert.Null(exception);
        }

        [NeedsDockerForCIFact]
        public async Task GetBomAgeAsync_ProvidesValue()
        {
            var service = await CreateRepoService();
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.StoreAsync(bom);
            var actual = await service.GetBomAgeAsync(bom.SerialNumber, bom.Version.Value);
            Assert.True(actual.Ticks < DateTime.Now.Ticks);
        }
    }

    public class MinioFixture : IAsyncLifetime, IDisposable
    {
        private TestcontainersContainer _testContainer;
        public AmazonS3Client S3Client { get; private set; }
        public string BucketName { get; private set; }

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
            S3Client = new AmazonS3Client(awsCredentials, s3Config);
            BucketName = $"bomserver{Guid.NewGuid()}";
        }

        public async Task DisposeAsync()
        {
            try
            {
                await _testContainer.StopAsync();
            }
#pragma warning disable CS0168
            catch (InvalidOperationException invalidOperationException)
#pragma warning restore CS0168
            {
            }
        }

        public void Dispose()
        {
            S3Client?.Dispose();
        }
    }
}