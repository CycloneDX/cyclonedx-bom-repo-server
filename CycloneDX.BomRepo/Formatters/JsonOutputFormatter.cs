using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using CycloneDX;

namespace CycloneDX.BomRepo.Formatters
{
    public class JsonOutputFormatter : TextOutputFormatter
    {
        public JsonOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.3"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.2"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanWriteType(Type type)
        {
            if (typeof(Models.v1_3.Bom).IsAssignableFrom(type))
            {
                return base.CanWriteType(type);
            }

            return false;
        }

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            var contentType = new ContentType(context.ContentType.ToString());
            
            var version = SchemaVersion.v1_3;
            if (contentType.Parameters?.ContainsKey("version") == true)
            {
                version = Enum.Parse<SchemaVersion>("v" + contentType.Parameters["version"].Replace('.', '_'));
            }
            
            var response = context.HttpContext.Response;
            var bom_v1_3 = context.Object as Models.v1_3.Bom;
            string bomJson;

            if (version == SchemaVersion.v1_2)
            {
                var bom_v1_2 = new Models.v1_2.Bom(bom_v1_3);
                bomJson = Json.Serializer.Serialize(bom_v1_2);
            }
            else
            {
                bomJson = Json.Serializer.Serialize(bom_v1_3);
            }

            await response.WriteAsync(bomJson);
        }

    }
}
