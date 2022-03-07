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
using System.Threading;
using System.Threading.Tasks;

namespace CycloneDX.BomRepoServer.Services
{
    class StorageMetadata
    {
        public int InternalStorageVersion { get; set; } 
    }

    public class OriginalBom : IDisposable
    {
        public Format Format { get; set; }
        public SpecificationVersion SpecificationVersion { get; set; }
        public Stream BomStream { get; set; }

        public void Dispose() => BomStream.Dispose();
    }
    
    public interface IRepoService
    {
        Task DeleteAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken));
        Task DeleteAllAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken));
        IAsyncEnumerable<string> GetAllBomSerialNumbersAsync(CancellationToken cancellationToken = default(CancellationToken));
        IAsyncEnumerable<int> GetAllVersionsAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken));
        Task<DateTime> GetBomAgeAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken));
        Task<CycloneDX.Models.v1_3.Bom> RetrieveAsync(string serialNumber, int? version = null, CancellationToken cancellationToken = default(CancellationToken));
        IAsyncEnumerable<CycloneDX.Models.v1_3.Bom> RetrieveAllAsync(string serialNumber, CancellationToken cancellationToken = default(CancellationToken));
        Task<OriginalBom> RetrieveOriginalAsync(string serialNumber, int version, CancellationToken cancellationToken = default(CancellationToken));
        Task<CycloneDX.Models.v1_3.Bom> StoreAsync(CycloneDX.Models.v1_3.Bom bom, CancellationToken cancellationToken = default(CancellationToken));
        Task StoreOriginalAsync(string serialNumber, int version, System.IO.Stream bomStream, Format format, SpecificationVersion specificationVersion, CancellationToken cancellationToken = default(CancellationToken));
        Task PostConstructAsync();
    }
}