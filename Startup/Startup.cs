using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Audit.Core;
using Audit.Udp;
using Audit.Udp.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SpleeterAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Environment = env;
            Configuration = configuration;
        }

        public static bool IsWindows { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static IConfiguration Configuration { get; private set; }
        public static IWebHostEnvironment Environment { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Do not use CORS for linux, should be added by nginx 
            if (Environment.IsDevelopment() || IsWindows)
            { 
                services.AddCors();
            }

            services.AddControllers()
                .AddJsonOptions(json => {
                    json.JsonSerializerOptions.IgnoreNullValues = true;
                    json.JsonSerializerOptions.WriteIndented = true;
                });

            services.AddSingleton<Youtube.YoutubeProcessor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            ConfigureAuditNet();

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Do not use HTTPS redirection in linux, since it will be done via nginx 
            if (Environment.IsDevelopment() || IsWindows)
            { 
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            // Do not use CORS for linux, should be added by nginx 
            if (Environment.IsDevelopment() || IsWindows)
            {
                app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            }

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigureAuditNet()
        {
            Audit.Core.Configuration.Setup()
                .UseUdp(_ => _
                    .RemoteAddress("127.0.0.1")
                    .RemotePort(2223)
                    .CustomSerializer(ev =>
                    {
                        if (ev.EventType == "Ephemeral")
                        {
                            return Encoding.UTF8.GetBytes(ev.CustomFields["Status"] as string);
                        }
                        else
                        {
                            return Encoding.UTF8.GetBytes(ev.ToJson());
                        }
                    }));
            
            EphemeralLog($"Spleeter started at {DateTime.Now}. ENV: {Environment.EnvironmentName}", true);
        }

        private static Regex _logFilterRegex = new Regex(@"\[download\]\s.*\sETA\s");

        public static void EphemeralLog(string text, bool important)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            if (!important)
            {
                if (text.Contains(" FutureWarning: "))
                {
                    return;
                }
                if (_logFilterRegex.IsMatch(text))
                {
                    return;
                }
            }

            Console.WriteLine(text);
            Audit.Core.AuditScope.CreateAndSave("Ephemeral", new { Status = text });
        }

    }
}
