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
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepoServer.Formatters
{
    public class ProtobufInputFormatter : TextInputFormatter
    {
        public ProtobufInputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf; version=1.4"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf; version=1.3"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanReadType(Type type) => type == typeof(CycloneDX.Models.Bom);

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding effectiveEncoding)
        {
            try
            {
                // TODO remove this once the .NET library supports reading from HTTP request streams
                // it currently tries to set the stream position to 0 without checking if that is supported by the stream instance
                var ms = new MemoryStream();
                await context.HttpContext.Request.Body.CopyToAsync(ms);
                ms.Position = 0;
                
                var bom = Protobuf.Serializer.Deserialize(ms);
                return await InputFormatterResult.SuccessAsync(bom);
            }
            catch (Exception)
            {
                return await InputFormatterResult.FailureAsync();
            }
        }
    }
}
