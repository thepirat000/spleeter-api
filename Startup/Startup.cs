using System;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Audit.Core;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;
using Microsoft.OpenApi.Models;
using System.Linq;

namespace SpleeterAPI
{
    public class Startup
    {

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Environment = env;
            Configuration = configuration;
        }

        private static Regex _logFilterRegex = new Regex(@"\[download\]\s.*\sETA\s|\s\=\snp\.dtype\(\[\(");
        public static bool IsWindows { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static JsonSerializerSettings JsonSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
        public static IConfiguration Configuration { get; private set; }
        public static IWebHostEnvironment Environment { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Do not use CORS for linux, should be added by nginx 
            if (IsWindows && Configuration["cors_enabled"].ToLower() == "true")
            { 
                services.AddCors();
            }

            services.AddControllers()
                .AddJsonOptions(json => {
                    json.JsonSerializerOptions.IgnoreNullValues = true;
                    json.JsonSerializerOptions.WriteIndented = true;
                });

            services.AddSingleton<Youtube.YoutubeProcessor>();
            services.AddHttpContextAccessor();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Version = "v1",
                    Title = "Spleeter API",
                    Description = "Audio separation API using Spleeter from Deezer"
                });
                var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly).ToList();
                xmlFiles.ForEach(xmlFile => c.IncludeXmlComments(xmlFile));
                c.DescribeAllParametersInCamelCase();
            });
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
            if (IsWindows && Configuration["https_enabled"].ToLower() == "true")
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            // Do not use CORS for linux, should be added by nginx 
            if (IsWindows && Configuration["cors_enabled"].ToLower() == "true")
            {
                app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            }

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Spleeter API");
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
                            var action = (ev as Audit.WebApi.AuditEventWebApi)?.Action;
                            var msg = $"Action: {action.ControllerName}/{action.ActionName}{new Uri(action.RequestUrl).Query} - Response: {action.ResponseStatusCode} {action.ResponseStatus}: {JsonConvert.SerializeObject(action.ResponseBody?.Value, JsonSettings)}. Event: {action.ToJson()}";
                            return Encoding.UTF8.GetBytes(msg);
                        }
                    }));
            
            EphemeralLog($"Spleeter started at {DateTime.Now}. ENV: {Environment.EnvironmentName}", true);
        }

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

        private static object _fleLogLocker = new object();
        public static void FileLog(string text, bool noAppend = false)
        {
            var logFile = GetFileLogPath();
            if (logFile == null)
            {
                return;
            }
            lock (_fleLogLocker)
            {
                if (noAppend)
                {
                    if (!File.Exists(logFile))
                    {
                        File.WriteAllText(logFile, text + System.Environment.NewLine);
                    }
                }
                else
                {
                    File.AppendAllText(logFile, text + System.Environment.NewLine);
                }
            }
        }
        public static string GetFileLogPath()
        {
            var logPath = Configuration["FileLogPath"];
            if (string.IsNullOrEmpty(logPath))
            {
                return null;
            }
            return Path.Combine(logPath, $"{DateTime.Now:yyyyMMdd}.log");
        }

    }
}
