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
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/x.vnd.cyclonedx+protobuf; version=1.3"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanReadType(Type type) => type == typeof(Models.v1_3.Bom);

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding effectiveEncoding)
        {
            try
            {
                var bom = Protobuf.Deserializer.Deserialize(context.HttpContext.Request.Body);
                return await InputFormatterResult.SuccessAsync(bom);
            }
            catch (Exception)
            {
                return await InputFormatterResult.FailureAsync();
            }
        }
    }
}
