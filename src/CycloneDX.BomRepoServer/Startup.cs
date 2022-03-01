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
using CycloneDX.BomRepoServer.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Amazon.S3;
using Amazon;
using Microsoft.VisualBasic;
using FileSystem = System.IO.Abstractions.FileSystem;

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
        public async void ConfigureServices(IServiceCollection services)
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
            IRepoService repoService;
            if(repoOptions.StorageType.Equals("FileSystem")) {
                var fileSystemRepoOptions = new FileSystemRepoOptions();
                Configuration.GetSection("Repo:Options").Bind(fileSystemRepoOptions);
                repoService = new FileSystemRepoService(new FileSystem(), fileSystemRepoOptions);
            } else if (repoOptions.StorageType.Equals("S3"))
            {
                var s3ClientOptions = Configuration.GetSection("Repo:Options").Get<S3ClientOptions>();
                var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(s3ClientOptions.AccessKey, s3ClientOptions.SecretKey);
                var s3Config = new AmazonS3Config()
                {
                    ForcePathStyle = s3ClientOptions.ForcePathStyle,
                    UseHttp = s3ClientOptions.UseHttp,
                    AuthenticationRegion = s3ClientOptions.Region,
                };
                if (s3ClientOptions.Endpoint != string.Empty)
                {
                    var protocol = s3ClientOptions.UseHttp ? "http" : "https";
                    s3Config.ServiceURL = $"{protocol}://{s3ClientOptions.Endpoint}";
                }
                
                var s3Client = new AmazonS3Client(awsCredentials, s3Config);
                repoService = new S3RepoService(s3Client, s3ClientOptions.BucketName);
            } else {
                throw new InvalidOperationException("Missing or unsupported storage type"); // TODO Validation filter https://andrewlock.net/adding-validation-to-strongly-typed-configuration-objects-in-asp-net-core/
            }
            
            services.AddSingleton(repoService);
            
            var bomCacheService = new CacheService(repoService);
            services.AddSingleton(bomCacheService);
            
            var retentionOptions = new RetentionOptions();
            Configuration.GetSection("Retention").Bind(retentionOptions);
            var bomRetentionService = new RetentionService(retentionOptions, repoService);
            services.AddSingleton(bomRetentionService);

            services.AddHostedService<CacheUpdateBackgroundService>();
            services.AddHostedService<RetentionBackgroundService>();
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
