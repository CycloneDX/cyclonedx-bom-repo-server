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

using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class CacheServiceTests
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
        
        [Fact]
        public async void SearchByGroupName_ReturnsAll()
        {
            var repoService = await CreateRepoService();
            await repoService.StoreAsync(new Bom
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
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:6e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "Canary"
                    }
                }
            });
            var service = new CacheService(repoService);
            await service.UpdateCache();

            var bomIdentifiers = service.Search(group: "Test").ToList();
            bomIdentifiers.Sort();
            
            Assert.Collection(bomIdentifiers, 
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                }
            );
        }
        
        [Fact]
        public async void SearchByName_ReturnsAll()
        {
            var repoService = await CreateRepoService();
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Name = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Name = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Name = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:6e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Name = "Canary"
                    }
                }
            });
            var service = new CacheService(repoService);
            await service.UpdateCache();

            var bomIdentifiers = service.Search(name: "Test").ToList();
            bomIdentifiers.Sort();
            
            Assert.Collection(bomIdentifiers, 
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                }
            );
        }

        [Fact]
        public async void SearchByVersion_ReturnsAll()
        {
            var repoService = await CreateRepoService();
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Version = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Version = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Version = "Test"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:6e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Version = "Canary"
                    }
                }
            });
            var service = new CacheService(repoService);
            await service.UpdateCache();

            var bomIdentifiers = service.Search(version: "Test").ToList();
            bomIdentifiers.Sort();
            
            Assert.Collection(bomIdentifiers, 
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                },
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                }
            );
        }

        [Fact]
        public async void CompositeSearch_ReturnsSingle()
        {
            var repoService = await CreateRepoService();
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "ACME",
                        Name = "Thing",
                        Version = "1"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:4e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "ACME",
                        Name = "Thing",
                        Version = "2"
                    }
                }
            });
            await repoService.StoreAsync(new Bom
            {
                SerialNumber = "urn:uuid:5e671687-395b-41f5-a30f-a58921a69b79",
                Version = 1,
                Metadata = new Metadata
                {
                    Component = new Component
                    {
                        Group = "ACME",
                        Name = "Thing",
                        Version = "3"
                    }
                }
            });
            var service = new CacheService(repoService);
            await service.UpdateCache();

            var bomIdentifiers = service.Search("ACME", "Thing", "1").ToList();
            bomIdentifiers.Sort();
            
            Assert.Collection(bomIdentifiers, 
                bomIdentifier =>
                {
                    Assert.Equal("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", bomIdentifier.SerialNumber);
                }
            );
        }
    }
}
