using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using CycloneDX.BomRepoServer.Formatters;
using CycloneDX.BomRepoServer.Options;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace CycloneDX.BomRepoServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers(config =>
            {
                config.RespectBrowserAcceptHeader = true;
                config.ReturnHttpNotAcceptable = true;
            }).AddMvcOptions(c =>
            {
                c.OutputFormatters.Clear();
                c.OutputFormatters.Add(new JsonOutputFormatter());
                c.OutputFormatters.Add(new XmlOutputFormatter());
                c.OutputFormatters.Add(new ProtobufOutputFormatter());
                c.OutputFormatters.Add(new SystemTextJsonOutputFormatter(new JsonSerializerOptions()));
                c.InputFormatters.Clear();
                c.InputFormatters.Add(new JsonInputFormatter());
                c.InputFormatters.Add(new XmlInputFormatter());
                c.InputFormatters.Add(new ProtobufInputFormatter());
            });
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "CycloneDX BOM Repository Server", Version = "v1" });
            });

            var allowedMethodsOptions = new AllowedMethodsOptions();
            Configuration.GetSection("AllowedMethods").Bind(allowedMethodsOptions);
            services.AddSingleton(allowedMethodsOptions);

            var repoOptions = new RepoOptions();
            Configuration.GetSection("Repo").Bind(repoOptions);
            services.AddSingleton(repoOptions);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CycloneDX BOM Repository Server v1"));

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
