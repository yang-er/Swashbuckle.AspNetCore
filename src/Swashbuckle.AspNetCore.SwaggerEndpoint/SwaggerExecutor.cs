using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer
{
    public class SwaggerExecutor
    {
        public OpenApiInfo Info { get; }
        private OpenApiDocument _document;
        private string _documentJson;
        private string _html;
        private readonly bool _asV2;
        private readonly IFileInfo _templateFile;

        public SwaggerExecutor(OpenApiInfo info, bool asV2, IFileInfo templateFile)
        {
            Info = info;
            _templateFile = templateFile;
            _asV2 = asV2;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.StatusCode = 200;
            response.ContentType = "application/json;charset=utf-8";

            if (_documentJson == null)
            {
                var swaggerGen = httpContext.RequestServices
                    .GetRequiredService<ISwaggerProvider>();
                _document = swaggerGen.GetSwagger(Info.Title.ToLower().Replace(' ', '_'));

                using var textWriter = new StringWriter(CultureInfo.InvariantCulture);
                var jsonWriter = new OpenApiJsonWriter(textWriter);
                if (_asV2) _document.SerializeAsV2(jsonWriter);
                else _document.SerializeAsV3(jsonWriter);

                _documentJson = textWriter.ToString();
            }

            await response.WriteAsync(_documentJson, new UTF8Encoding(false));
        }

        public async Task InvokeAsHtml(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.StatusCode = 200;
            response.ContentType = "text/html;charset=utf-8";

            if (_html == null)
            {
                var swaggerGen = httpContext.RequestServices
                    .GetRequiredService<ISwaggerProvider>();
                _document = swaggerGen.GetSwagger(Info.Title.ToLower().Replace(' ', '_'));

                if (!_templateFile.Exists) throw new InvalidDataException();
                using var textWriter = new StringWriter(CultureInfo.InvariantCulture);

                using (var templateReader = _templateFile.CreateReadStream())
                using (var textReader = new StreamReader(templateReader))
                {
                    while (true)
                    {
                        string htmlContent = await textReader.ReadLineAsync();
                        if (htmlContent == null) break;
                        if (htmlContent.Contains("<%TITLE%>"))
                            htmlContent.Replace("<%TITLE%>", Info.Title);

                        if (!htmlContent.Contains("<%SPEC%>"))
                        {
                            textWriter.WriteLine(htmlContent);
                            continue;
                        }

                        var titleIndex = htmlContent.IndexOf("<%SPEC%>");
                        textWriter.Write(htmlContent.Substring(0, titleIndex));
                        var jsonWriter = new OpenApiJsonWriter(textWriter);
                        _document.SerializeAsV2(jsonWriter);
                        textWriter.WriteLine(htmlContent.Substring(titleIndex + 8));
                    }
                }

                _html = textWriter.ToString();
            }

            await response.WriteAsync(_html, new UTF8Encoding(false));
        }
    }
}
