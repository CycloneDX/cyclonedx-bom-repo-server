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
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepoServer.Formatters
{
    public class JsonOutputFormatter : TextOutputFormatter
    {
        public JsonOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.4"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.3"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.2"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanWriteType(Type type) => type == typeof(CycloneDX.Models.Bom);

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var contentType = new ContentType(context.ContentType.ToString());
            
            var version = SpecificationVersion.v1_4;
            if (contentType.Parameters?.ContainsKey("version") == true)
            {
                version = Enum.Parse<SpecificationVersion>("v" + contentType.Parameters["version"].Replace('.', '_'));
            }
            
            var response = context.HttpContext.Response;
            var bom = context.Object as CycloneDX.Models.Bom;
            bom.SpecVersion = version;

            string bomJson = Json.Serializer.Serialize(bom);

            await response.WriteAsync(bomJson);
        }

    }
}
