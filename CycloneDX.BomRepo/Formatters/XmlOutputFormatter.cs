using System;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CycloneDX.BomRepo.Formatters
{
    public class XmlOutputFormatter : TextOutputFormatter
    {
        public XmlOutputFormatter()
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

        protected override bool CanWriteType(Type type) => type == typeof(Models.v1_3.Bom);

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
            string bomXml;

            if (version == SchemaVersion.v1_2)
            {
                var bom_v1_2 = new Models.v1_2.Bom(bom_v1_3);
                bomXml = Xml.Serializer.Serialize(bom_v1_2);
            }
            else if (version == SchemaVersion.v1_1)
            {
                var bom_v1_2 = new Models.v1_2.Bom(bom_v1_3);
                var bom_v1_1 = new Models.v1_1.Bom(bom_v1_2);
                bomXml = Xml.Serializer.Serialize(bom_v1_1);
            }
            else if (version == SchemaVersion.v1_0)
            {
                var bom_v1_2 = new Models.v1_2.Bom(bom_v1_3);
                var bom_v1_1 = new Models.v1_1.Bom(bom_v1_2);
                var bom_v1_0 = new Models.v1_0.Bom(bom_v1_1);
                bomXml = Xml.Serializer.Serialize(bom_v1_0);
            }
            else
            {
                bomXml = Xml.Serializer.Serialize(bom_v1_3);
            }

            await response.WriteAsync(bomXml);
        }

    }
}
