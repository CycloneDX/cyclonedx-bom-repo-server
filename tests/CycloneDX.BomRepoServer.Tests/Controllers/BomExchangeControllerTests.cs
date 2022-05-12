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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CycloneDX.BomRepoServer.Tests.Controllers
{
    public class BomExchangeControllerTests
    {
        private async Task<WebApplicationFactory<Startup>> GetTestServerFactory(string repoDirectory, AllowedMethodsOptions allowedMethods)
        {
            var factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                    {
                        configBuilder.AddInMemoryCollection(
                            new Dictionary<string, string>
                            {
                                {"Repo:StorageType", "FileSystem"},
                                {"Repo:Options:Directory", repoDirectory},
                                {"AllowedMethods:Get", allowedMethods.Get ? "true" : "false"},
                            });
                    })
                    .ConfigureTestServices(collection =>
                    {
                        collection.AddSingleton<RepoMetadataHostedService>();
                    });
            });
            await factory.Services.GetRequiredService<RepoMetadataHostedService>().StartAsync(CancellationToken.None);
            return factory;
        }

        [Theory]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79")]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/0")]
        public async Task GetBomWithInvalidBomIdentifierReturnsBadRequest(string bomIdentifier)
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            await service.StoreAsync(bom);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bomexchange?bomIdentifier={bomIdentifier}");

            var result = await client.SendAsync(request);
            
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
        
        [Theory]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/2")]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b80/1")]
        public async Task GetBomWithNonExistentBomIdentifierReturnsNotFound(string bomIdentifier)
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            await service.StoreAsync(bom);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bomexchange?bomIdentifier={bomIdentifier}");

            var result = await client.SendAsync(request);
            
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }
        
        [Fact]
        public async Task GetBomBySerialNumberReturnsLatestVersion()
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            await service.StoreAsync(bom);
            bom.Version = 2;
            await service.StoreAsync(bom);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bomexchange?bomIdentifier={bom.SerialNumber}");

            var response = await client.SendAsync(request);

            Assert.True(response.IsSuccessStatusCode);

            var bomContents = await response.Content.ReadAsStringAsync();
            var retrievedBom = CycloneDX.Json.Serializer.Deserialize(bomContents);
            
            Assert.Equal(bom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(bom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public async Task GetBomByCdxUrnReturnsBom()
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            await service.StoreAsync(bom);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bomexchange?bomIdentifier=urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/1");

            var response = await client.SendAsync(request);

            Assert.True(response.IsSuccessStatusCode);

            var bomContents = await response.Content.ReadAsStringAsync();
            var retrievedBom = CycloneDX.Json.Serializer.Deserialize(bomContents);
            
            Assert.Equal(bom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(bom.Version, retrievedBom.Version);
        }

        [Theory]
        [InlineData("text/xml", null)]
        [InlineData("application/xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", "1.4")]
        [InlineData("application/vnd.cyclonedx+xml", "1.3")]
        [InlineData("application/vnd.cyclonedx+xml", "1.2")]
        [InlineData("application/vnd.cyclonedx+xml", "1.1")]
        [InlineData("application/vnd.cyclonedx+xml", "1.0")]
        [InlineData("application/json", null)]
        [InlineData("application/vnd.cyclonedx+json", null)]
        [InlineData("application/vnd.cyclonedx+json", "1.4")]
        [InlineData("application/vnd.cyclonedx+json", "1.3")]
        [InlineData("application/vnd.cyclonedx+json", "1.2")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", null)]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.4")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.3")]
        public async Task GetBom_ReturnsCorrectContentType(string mediaType, string version)
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            await service.StoreAsync(bom);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bomexchange?bomIdentifier={bom.SerialNumber}");
            if (version != null)
                request.Headers.Accept.ParseAdd($"{mediaType}; version={version}");
            else
                request.Headers.Accept.ParseAdd(mediaType);

            var result = await client.SendAsync(request);
            
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(mediaType, result.Content.Headers.ContentType.MediaType);

            if (version != null)
            {
                var versionParameterFound = false;
                foreach (var parameter in result.Content.Headers.ContentType.Parameters)
                {
                    if (parameter.Name == "version")
                    {
                        Assert.Equal(version, parameter.Value);
                        versionParameterFound = true;
                        break;
                    }
                }
                Assert.True(versionParameterFound);
            }
        }
        
        [Theory]
        [InlineData("text/xml", null)]
        [InlineData("application/xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", "1.4")]
        [InlineData("application/vnd.cyclonedx+xml", "1.3")]
        [InlineData("application/vnd.cyclonedx+xml", "1.2")]
        [InlineData("application/vnd.cyclonedx+xml", "1.1")]
        [InlineData("application/vnd.cyclonedx+xml", "1.0")]
        [InlineData("application/json", null)]
        [InlineData("application/vnd.cyclonedx+json", null)]
        [InlineData("application/vnd.cyclonedx+json", "1.4")]
        [InlineData("application/vnd.cyclonedx+json", "1.3")]
        [InlineData("application/vnd.cyclonedx+json", "1.2")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", null)]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.4")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.3")]
        public async Task PostBom_StoresBom(string mediaType, string version)
        {
            using var tmpDirectory = new TempDirectory();
            var factory = await GetTestServerFactory(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });
            var service = factory.Services.GetRequiredService<FileSystemRepoService>();
            var client = factory.CreateClient();
            
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };

            var contentType = mediaType;
            if (version != null)
                contentType += $"; version={version}";
            var contentTypeHeader = MediaTypeHeaderValue.Parse(contentType);

            var request = new HttpRequestMessage(HttpMethod.Post, "/bomexchange");
            
            if (mediaType == MediaTypes.Protobuf || mediaType == "application/octet-stream")
            {
                var bomArray = Protobuf.Serializer.Serialize(bom);
                request.Content = new ByteArrayContent(bomArray);
                request.Content.Headers.ContentType = contentTypeHeader;
            }
            else
            {
                if (mediaType == MediaTypes.Xml || mediaType.EndsWith("xml"))
                {
                    request.Content = new StringContent(Xml.Serializer.Serialize(bom), Encoding.UTF8);
                    request.Content.Headers.ContentType = contentTypeHeader;
                }
                else if (mediaType == MediaTypes.Json || mediaType.EndsWith("json"))
                {
                    request.Content = new StringContent(Json.Serializer.Serialize(bom), Encoding.UTF8);
                    request.Content.Headers.ContentType = contentTypeHeader;
                }
            }

            var result = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, result.StatusCode);

            var storedBom = await service.RetrieveAsync(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, storedBom.SerialNumber);
            Assert.Equal(bom.Version, storedBom.Version);
        }

        [Theory]
        [InlineData(" urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/1")]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/1 ")]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/0")]
        [InlineData("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b7")]
        public void ValidCdxUrn_RecognisesInvalidValues(string serialNumber)
        {
            Assert.False(BomExchangeController.ValidCdxUrn(serialNumber));
        }

        [Fact]
        public void ParseCdxUrnTest()
        {
            var result = BomExchangeController.ParseCdxUrn("urn:cdx:3e671687-395b-41f5-a30f-a58921a69b79/1");

            Assert.Equal("3e671687-395b-41f5-a30f-a58921a69b79", result.Item1);
            Assert.Equal(1, result.Item2);
        }
    }
}
