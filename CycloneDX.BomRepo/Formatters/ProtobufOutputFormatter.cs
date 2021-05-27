using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepo.Formatters
{
    public class ProtobufOutputFormatter : OutputFormatter
    {
        public ProtobufOutputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/octet-stream"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf; version=1.3"));
        }

        protected override bool CanWriteType(Type type)
        {
            if (typeof(Models.v1_3.Bom).IsAssignableFrom(type))
            {
                return base.CanWriteType(type);
            }

            return false;
        }

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            var response = context.HttpContext.Response;
            var bom_v1_3 = context.Object as Models.v1_3.Bom;

            var bomProtobuf = Protobuf.Serializer.Serialize(bom_v1_3);

            await response.Body.WriteAsync(bomProtobuf);
            
            Protobuf.Serializer.Serialize(response.Body, bom_v1_3);
        }
    }
}
