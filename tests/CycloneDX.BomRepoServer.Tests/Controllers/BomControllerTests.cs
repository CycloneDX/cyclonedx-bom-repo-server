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
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CycloneDX.BomRepoServer.Tests.Controllers
{
    public class BomControllerTests
    {
        private HttpClient GetWebApplicationClient(string repoDirectory, AllowedMethodsOptions allowedMethods)
        {
            var factory = new WebApplicationFactory<Startup>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string>
                        {
                            { "Repo:Directory", repoDirectory },
                            { "AllowedMethods:Get", allowedMethods.Get ? "true" : "false" },
                        });
                });
            });
            return factory.CreateClient();
        }
        
        [Theory]
        [InlineData("text/xml", null)]
        [InlineData("application/xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", null)]
        [InlineData("application/vnd.cyclonedx+xml", "1.3")]
        [InlineData("application/vnd.cyclonedx+xml", "1.2")]
        [InlineData("application/vnd.cyclonedx+xml", "1.1")]
        [InlineData("application/vnd.cyclonedx+xml", "1.0")]
        [InlineData("application/json", null)]
        [InlineData("application/vnd.cyclonedx+json", null)]
        [InlineData("application/vnd.cyclonedx+json", "1.3")]
        [InlineData("application/vnd.cyclonedx+json", "1.2")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", null)]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.3")]
        public async Task GetBom_ReturnsCorrectContentType(string mediaType, string version)
        {
            using var tmpDirectory = new TempDirectory();

            var options = new RepoOptions
            {
                Directory = tmpDirectory.DirectoryPath
            };
            var service = new FileSystemRepoService(new FileSystem(), options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };
            service.Store(bom);

            var client = GetWebApplicationClient(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true });

            var request = new HttpRequestMessage(HttpMethod.Get, $"/bom?serialNumber={bom.SerialNumber}&version={bom.Version}");
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
        [InlineData("application/vnd.cyclonedx+xml", "1.3")]
        [InlineData("application/vnd.cyclonedx+xml", "1.2")]
        [InlineData("application/vnd.cyclonedx+xml", "1.1")]
        [InlineData("application/vnd.cyclonedx+xml", "1.0")]
        [InlineData("application/json", null)]
        [InlineData("application/vnd.cyclonedx+json", null)]
        [InlineData("application/vnd.cyclonedx+json", "1.3")]
        [InlineData("application/vnd.cyclonedx+json", "1.2")]
        [InlineData("application/x.vnd.cyclonedx+protobuf", null)]
        [InlineData("application/x.vnd.cyclonedx+protobuf", "1.3")]
        public async Task PostBom_StoresBom(string mediaType, string version)
        {
            using var tmpDirectory = new TempDirectory();

            var options = new RepoOptions
            {
                Directory = tmpDirectory.DirectoryPath
            };
            var service = new FileSystemRepoService(new FileSystem(), options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1
            };

            var client = GetWebApplicationClient(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Post = true });

            var contentType = mediaType;
            if (version != null)
                contentType += $"; version={version}";
            var contentTypeHeader = MediaTypeHeaderValue.Parse(contentType);

            var request = new HttpRequestMessage(HttpMethod.Post, "/bom");
            
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

            var storedBom = service.Retrieve(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, storedBom.SerialNumber);
            Assert.Equal(bom.Version, storedBom.Version);
        }

        [Theory]
        [InlineData("xml")]
        [InlineData("json")]
        [InlineData("protobuf")]
        public async Task PostBom_And_RetrieveOriginalBom(string format)
        {
            Assert.True(Format.TryParse(format, true, out Format parsedFormat));
            using var tmpDirectory = new TempDirectory();

            var bom = new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79"
            };

            var client = GetWebApplicationClient(tmpDirectory.DirectoryPath, new AllowedMethodsOptions { Get = true, Post = true });

            var contentType = MediaTypes.GetMediaType(parsedFormat);
            var contentTypeHeader = MediaTypeHeaderValue.Parse(contentType);

            var request = new HttpRequestMessage(HttpMethod.Post, "/bom");
            byte[] originalBomBytes = null;
            string originalBomString = null;
            
            if (parsedFormat == Format.Protobuf)
            {
                originalBomBytes = Protobuf.Serializer.Serialize(bom);
            }
            else if (parsedFormat == Format.Xml)
            {
                originalBomString = Xml.Serializer.Serialize(bom);
                originalBomBytes = Encoding.UTF8.GetBytes(originalBomString);
            }
            else if (parsedFormat == Format.Json)
            {
                originalBomString = Json.Serializer.Serialize(bom);
                originalBomBytes = Encoding.UTF8.GetBytes(originalBomString);
            }

            request.Content = new ByteArrayContent(originalBomBytes);
            request.Content.Headers.ContentType = contentTypeHeader;

            var result = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, result.StatusCode);

            client.DefaultRequestHeaders.Add("Accept", contentType);
            var response = await client.GetAsync(result.Headers.Location + "&original=true");
            
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);

            var retrievedOriginalBom = await response.Content.ReadAsByteArrayAsync();

            if (originalBomString != null)
            {
                // For XML and JSON this will provide more useful output than the following assert
                var retrievedOriginalBomString = Encoding.UTF8.GetString(retrievedOriginalBom);
                Assert.Equal(originalBomString, retrievedOriginalBomString);
            }

            Assert.Equal(originalBomBytes, retrievedOriginalBom);
        }

        [Theory]
        [InlineData(" urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79")]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79 ")]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b7")]
        [InlineData(" {3e671687-395b-41f5-a30f-a58921a69b79}")]
        [InlineData("{3e671687-395b-41f5-a30f-a58921a69b79} ")]
        [InlineData("{3e671687-395b-41f5-a30f-a58921a69b7}")]
        public void ValidSerialNumber_RecognisesInvalidValues(string serialNumber)
        {
            Assert.False(BomController.ValidSerialNumber(serialNumber));
        }
    }
}
