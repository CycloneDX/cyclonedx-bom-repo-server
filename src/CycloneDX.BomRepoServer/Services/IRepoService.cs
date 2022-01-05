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
using System.Threading.Tasks;

namespace CycloneDX.BomRepoServer.Services
{
    public interface IRepoService
    {
        void Delete(string serialNumber, int version);
        void DeleteAll(string serialNumber);
        IEnumerable<string> GetAllBomSerialNumbers();
        IEnumerable<int> GetAllVersions(string serialNumber);
        DateTime GetBomAge(string serialNumber, int version);
        CycloneDX.Models.v1_3.Bom Retrieve(string serialNumber, int? version = null);
        List<CycloneDX.Models.v1_3.Bom> RetrieveAll(string serialNumber);
        OriginalBom RetrieveOriginal(string serialNumber, int version);
        CycloneDX.Models.v1_3.Bom Store(CycloneDX.Models.v1_3.Bom bom);
        Task StoreOriginal(string serialNumber, int version, System.IO.Stream bomStream, Format format, SpecificationVersion specificationVersion);
    }
}