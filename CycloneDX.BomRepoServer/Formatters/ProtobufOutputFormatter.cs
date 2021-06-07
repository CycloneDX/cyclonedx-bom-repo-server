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
// Copyright (c) Patrick Dwyer. All Rights Reserved.
    
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepoServer.Formatters
{
    public class ProtobufOutputFormatter : OutputFormatter
    {
        public ProtobufOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf; version=1.3"));
        }

        protected override bool CanWriteType(Type type) => type == typeof(CycloneDX.Models.v1_3.Bom);

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            var response = context.HttpContext.Response;
            var bom_v1_3 = context.Object as CycloneDX.Models.v1_3.Bom;

            var bomProtobuf = Protobuf.Serializer.Serialize(bom_v1_3);

            await response.Body.WriteAsync(bomProtobuf);
            
            Protobuf.Serializer.Serialize(response.Body, bom_v1_3);
        }
    }
}
