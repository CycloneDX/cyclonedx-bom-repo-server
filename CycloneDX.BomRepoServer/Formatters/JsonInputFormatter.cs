using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepoServer.Formatters
{
    public class JsonInputFormatter : TextInputFormatter
    {
        public JsonInputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.3"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+json; version=1.2"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanReadType(Type type) => type == typeof(Models.v1_3.Bom);

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding effectiveEncoding)
        {
            using var reader = new StreamReader(context.HttpContext.Request.Body, effectiveEncoding);

            var jsonString = await reader.ReadToEndAsync();

            try
            {
                var bom = Json.Deserializer.Deserialize(jsonString);
                return await InputFormatterResult.SuccessAsync(bom);
            }
            catch (Exception)
            {
                return await InputFormatterResult.FailureAsync();
            }
        }

    }
}
