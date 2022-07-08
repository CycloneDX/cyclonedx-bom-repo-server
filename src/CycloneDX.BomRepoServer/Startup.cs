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
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using CycloneDX.BomRepoServer.Formatters;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Amazon.S3;
using Microsoft.Extensions.Options;

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
            BindOptions(services);

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
                c.SwaggerDoc("v1", new OpenApiInfo {
                    Title = "CycloneDX BOM Repository Server",
                    Version = "v1",
                    Contact = new OpenApiContact
                    {
                        Name = "CycloneDX Community",
                        Url = new Uri("https://github.com/CycloneDX/cyclonedx-bom-repo-server")
                    },
                });
                // enable xml documentation
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<FileSystemRepoService>();

            // overwrite to force use lowercase
            services.AddRouting(options => options.LowercaseUrls = true);
            
            services.AddSingleton<IAmazonS3, AmazonS3Client>(provider =>
            {
                var s3ClientOptions = provider.GetRequiredService<S3ClientOptions>();
                var awsCredentials =
                    new Amazon.Runtime.BasicAWSCredentials(s3ClientOptions.AccessKey, s3ClientOptions.SecretKey);
                var s3Config = new AmazonS3Config
                {
                    ForcePathStyle = s3ClientOptions.ForcePathStyle,
                    UseHttp = s3ClientOptions.UseHttp,
                    AuthenticationRegion = s3ClientOptions.Region,
                };

                if (s3ClientOptions.Endpoint == string.Empty) return new AmazonS3Client(awsCredentials, s3Config);
                var protocol = s3ClientOptions.UseHttp ? "http" : "https";
                s3Config.ServiceURL = $"{protocol}://{s3ClientOptions.Endpoint}";
                return new AmazonS3Client(awsCredentials, s3Config);
            });
            
            services.AddSingleton(provider =>
            {
                var s3Client = provider.GetRequiredService<IAmazonS3>();
                var s3ClientOptions = provider.GetRequiredService<S3ClientOptions>();
                    
                return new S3RepoService(s3Client, s3ClientOptions.BucketName);
            });
            
            services.AddHostedService<RepoMetadataHostedService>();
            
            services.AddSingleton<IRepoService>(provider =>
            {
                var repoOptions = provider.GetRequiredService<RepoOptions>();
                return repoOptions.StorageType switch
                {
                    "FileSystem" => provider.GetRequiredService<FileSystemRepoService>(),
                    "S3" => provider.GetRequiredService<S3RepoService>(),
                    _ => throw new ArgumentOutOfRangeException(nameof(repoOptions.StorageType))
                };
            });
            
            services.AddSingleton<CacheService>();
            services.AddSingleton<RetentionService>();

            services.AddHostedService<CacheUpdateBackgroundService>();
            services.AddHostedService<RetentionBackgroundService>();
        }

        private void BindOptions(IServiceCollection services)
        {
            services.AddOptions<RetentionOptions>().Bind(Configuration.GetSection("Retention"));
            services.AddOptions<AllowedMethodsOptions>().Bind(Configuration.GetSection("AllowedMethods"));
            services.AddOptions<RepoOptions>().Bind(Configuration.GetSection("Repo"));
            services.AddOptions<FileSystemRepoOptions>().Bind(Configuration.GetSection("Repo:Options"));
            services.AddOptions<S3ClientOptions>().Bind(Configuration.GetSection("Repo:Options"));

            services.AddSingleton(provider => provider.GetRequiredService<IOptions<RetentionOptions>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<AllowedMethodsOptions>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<RepoOptions>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<FileSystemRepoOptions>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<S3ClientOptions>>().Value);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "CycloneDX BOM Repository Server v1");
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}