using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepo.Formatters
{
    public class XmlInputFormatter : TextInputFormatter
    {
        public XmlInputFormatter()
        {
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/xml"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/xml"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+xml"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+xml; version=1.3"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+xml; version=1.2"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+xml; version=1.1"));
            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/vnd.cyclonedx+xml; version=1.0"));
            SupportedEncodings.Add(Encoding.UTF8);
            SupportedEncodings.Add(Encoding.Unicode);
        }

        protected override bool CanReadType(Type type) => type == typeof(Models.v1_3.Bom);

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding effectiveEncoding)
        {
            using var reader = new StreamReader(context.HttpContext.Request.Body, effectiveEncoding);

            var xmlString = await reader.ReadToEndAsync();

            try
            {
                var bom = Xml.Deserializer.Deserialize(xmlString);
                return await InputFormatterResult.SuccessAsync(bom);
            }
            catch (Exception)
            {
                return await InputFormatterResult.FailureAsync();
            }
        }

    }
}
