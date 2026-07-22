using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime.Internal.Util;
using LHSDBFreeAgentsAPI.Mappers;
using LHSDBFreeAgentsAPI.Repositories;
using LHSDBFreeAgentsAPI.Repositories.DynamoDBImpl;
using LHSDBFreeAgentsAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace LHSDBFreeAgentsAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        private static readonly HttpClient _httpClient = new HttpClient();

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var Region = Configuration["AWSCognito:Region"];
            var PoolId = Configuration["AWSCognito:PoolId"];
            var AppClientId = Configuration["AWSCognito:AppClientId"];

             services
             .AddAuthentication(options =>
             {
                 options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                 options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
             })
             .AddJwtBearer(options =>
             {
                 options.SaveToken = true;
                 options.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuerSigningKey = true,
                     IssuerSigningKeyResolver = (s, securityToken, identifier, parameters) =>
                     {
                         // Get JsonWebKeySet from AWS
                         var json = _httpClient.GetStringAsync(parameters.ValidIssuer + "/.well-known/jwks.json").GetAwaiter().GetResult();
                         // Serialize the result
                         return JsonConvert.DeserializeObject<JsonWebKeySet>(json).Keys;
                     },
                     ValidateIssuer = true,
                     ValidIssuer = $"https://cognito-idp.{Region}.amazonaws.com/{PoolId}",
                     ValidateLifetime = true,
                     LifetimeValidator = (before, expires, token, param) => expires > DateTime.UtcNow,
                     ValidateAudience = false,
                     ValidAudience = AppClientId,
                 };
             });

            services
            .AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy => policy.RequireClaim("custom:isAdmin", new List<string> { "1" }));
            });

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                                  builder =>
                                  {
                                      builder.WithOrigins("http://localhost:4200", 
                                          "https://agentslibres.piriwin.com", 
                                          "https://lhsdb-fa-api.piriwin.com")
                                          .AllowAnyMethod()
                                      .AllowAnyHeader()
                                      ;
                                  });
            });

            services.AddAWSService<IAmazonDynamoDB>();
            services.AddDefaultAWSOptions(
                new AWSOptions
                {
                    Region = RegionEndpoint.GetBySystemName("us-east-2")
                });

            services.AddHealthChecks();
            services.AddControllers().AddNewtonsoftJson();
            services.AddControllersWithViews().AddNewtonsoftJson();
            services.AddRazorPages().AddNewtonsoftJson();

            services.AddSingleton<IPlayerService, PlayerService>();
            services.AddSingleton<IPlayerRepository, PlayerRepository>();
            services.AddSingleton<IOfferService, OfferService>();
            services.AddSingleton<IOfferRepository, OfferRepository>();
            services.AddSingleton<IMapper, Mapper>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            var config = this.Configuration.GetAWSLoggingConfigSection();
            loggerFactory.AddAWSProvider(config);

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");

            });
        }
    }
}
