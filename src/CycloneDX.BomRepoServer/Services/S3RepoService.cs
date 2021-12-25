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

//TODO need to make use of async methods once suitable methods have been added to the core library
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Exceptions;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.Models.v1_3;
using Microsoft.Extensions.Logging;
using Amazon.S3;

namespace CycloneDX.BomRepoServer.Services
{
    class S3RepoService : IRepoService
    {
        private readonly IAmazonS3 _s3Client;

        public S3RepoService(IAmazonS3 s3Client)
        {
            this._s3Client = s3Client;
        }

        public void Delete(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public void DeleteAll(string serialNumber)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetAllBomSerialNumbers()
        {
            
            var s3objects = _s3Client.ListObjectsAsync("bomserver").GetAwaiter().GetResult();
            return s3objects.S3Objects.Select(s3Object => {
                return s3Object.Key;
            });
        }

        public IEnumerable<int> GetAllVersions(string serialNumber)
        {
            throw new NotImplementedException();
        }

        public DateTime GetBomAge(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public Bom Retrieve(string serialNumber, int? version = null)
        {
            throw new NotImplementedException();
        }

        public List<Bom> RetrieveAll(string serialNumber)
        {
            throw new NotImplementedException();
        }

        public OriginalBom RetrieveOriginal(string serialNumber, int version)
        {
            throw new NotImplementedException();
        }

        public Stream RetrieveStream(string serialNumber, int? version = null)
        {
            throw new NotImplementedException();
        }

        public Bom Store(Bom bom)
        {
            throw new NotImplementedException();
        }

        public Task StoreOriginal(string serialNumber, int version, Stream bomStream, Format format, SpecificationVersion specificationVersion)
        {
            throw new NotImplementedException();
        }
    }
}