using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyRaspNet.Configuration;
using Microsoft.AspNetCore.ResponseCompression;
using MyRaspNet.Logging;
using MyRaspNet.Mqtt;
using MQTTnet.AspNetCore;
using System.Net;
using Microsoft.AspNetCore.SignalR;

namespace MyRaspNet
{
    public class Startup
    {
        public IWebHostEnvironment Env { get; }
        public IConfiguration Configuration { get; }
        private static bool enableMqqtWebSocket = false;
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            this.Env = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var settings = new AppSettings();
            var mqttClientSettings = new MqttClientSettings();
            var raspberrySettings = new RaspberrySettings();
            Configuration.Bind(Utils.CONFIGKEY_APPSETTINGS, settings);
            Configuration.Bind(Utils.CONFIGKEY_MQTTClient, mqttClientSettings);
            Configuration.Bind(Utils.CONFIGKEY_RASPBERRY, raspberrySettings);
            services.AddSingleton<AppSettings>(settings);
            services.AddSingleton<MqttClientSettings>(mqttClientSettings);
            services.AddSingleton<RaspberrySettings>(raspberrySettings);
            services.AddSingleton<WritableConfiguration>();
            services.AddSingleton<Hubs.DiagHub>();
            services.AddSingleton<Hubs.GPIOHub>();

            #region Compression
            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = System.IO.Compression.CompressionLevel.Optimal;
            });
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();

                options.MimeTypes = new[]
                {
                    "text/css",
                    "application/javascript",
                    "application/json",
                    "text/json",
                    "application/xml",
                    "text/xml",
                    "text/plain",
                    "image/svg+xml",
                    "application/x-font-ttf"
                };
            });
            #endregion

            #region Cache, Session, Cookie
            services.AddMemoryCache();
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(3);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.ConfigureApplicationCookie(options =>
            {
                // Cookie settings
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            // Cache 200 (OK) server responses; any other responses, including error pages, are ignored.
            services.AddResponseCaching();
            #endregion

            #region Device
            services.AddSingleton<RaspberryDevice>();
            #endregion


            #region MQTT
            var mqttSettings = new MqttSettingsModel();
            Configuration.Bind(Utils.CONFIGKEY_MQTTServer, mqttSettings);
            //services.ConfigureWritable<MqttSettingsModel>(Configuration.GetSection("MQTT"));
            services.AddSingleton(mqttSettings);
            services.AddSingleton<MqttNetLoggerWrapper>();
            services.AddSingleton<CustomMqttFactory>();
            services.AddSingleton<MqttServerService>();
            services.AddSingleton<MqttClientService>();
            services.AddSingleton<MqttServerStorage>();
            services.AddSingleton<MqttClientConnectedHandler>();
            services.AddSingleton<MqttClientDisconnectedHandler>();
            services.AddSingleton<MqttClientSubscribedTopicHandler>();
            services.AddSingleton<MqttClientUnsubscribedTopicHandler>();
            services.AddSingleton<MqttServerConnectionValidator>();
            services.AddSingleton<MqttSubscriptionInterceptor>();
            services.AddSingleton<MqttUnsubscriptionInterceptor>();
            services.AddSingleton<MqttApplicationMessageInterceptor>();

            #endregion

            #region HangFire
            //if (settings.EnableScheduler)
            //{
            //    //services.AddHangfire(configuration => configuration
            //    //       .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            //    //       .UseSimpleAssemblyNameTypeSerializer()
            //    //       .UseRecommendedSerializerSettings()
            //    //       .UseSqlServerStorage(settings.ConnectionStrings["HangfireConnection"], new SqlServerStorageOptions
            //    //       {
            //    //           CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            //    //           SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            //    //           QueuePollInterval = TimeSpan.Zero,
            //    //           UseRecommendedIsolationLevel = true,
            //    //           DisableGlobalLocks = true,
            //    //       }));



            //    //var mySqlStorage = new MySqlStorage(
            //    //    settings.ConnectionStrings["HangfireConnection"],
            //    //    new MySqlStorageOptions
            //    //    {
            //    //        TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
            //    //        QueuePollInterval = TimeSpan.FromSeconds(15),
            //    //        JobExpirationCheckInterval = TimeSpan.FromHours(1),
            //    //        CountersAggregateInterval = TimeSpan.FromMinutes(5),
            //    //        PrepareSchemaIfNecessary = true,
            //    //        DashboardJobListLimit = 50000,
            //    //        TransactionTimeout = TimeSpan.FromMinutes(3),
            //    //        TablesPrefix = "Hangfire"
            //    //    });
            //    //services.AddHangfire(configuration => configuration
            //    //    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            //    //        .UseSimpleAssemblyNameTypeSerializer()
            //    //        .UseRecommendedSerializerSettings()
            //    //        .UseStorage(mySqlStorage));


            //    //// Add the processing server as IHostedService
            //    //services.AddHangfireServer(options =>
            //    //{
            //    //    options.ServerName = "MyRasp Background Process";
            //    //    options.SchedulePollingInterval = TimeSpan.FromSeconds(3);
            //    //    options.Queues = new string[] { "default", "cleanup", "mqtt", "automation", "critical", "email" };
            //    //});

            //}
            #endregion


            var pages = services.AddRazorPages(c =>
            {
                c.RootDirectory = "/Pages";
            });
            pages.AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });
            services.AddSignalR();

            // Enable HttpContext reference to other
            services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.HttpContextAccessor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, AppSettings settings, MqttServerService mqttServerService, MqttClientService mqttClientService, MqttSettingsModel mqttSettings, RaspberryDevice device)
        {

            // required for linux environment
            app.UseForwardedHeaders(new ForwardedHeadersOptions()
            {
                ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            // Enable Auth 
            //app.UseAuthorization();

            // Use HTTPS.
            app.UseHttpsRedirection();

            // Use response compression.
            app.UseResponseCompression();

            device.Configure();
            ConfigureWebSocketEndpoint(app, mqttServerService, mqttSettings, device);
            mqttServerService.Configure();




            #region HangFire
            //if (settings.EnableScheduler)
            //{
            //    app.UseHangfireDashboard("/servicejob", new DashboardOptions
            //    {
            //        //Authorization = new[] { new SchedulerAuth() },
            //        DashboardTitle = "Service Job",
            //    });
            //}
            #endregion

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapHub<Hubs.DiagHub>("/ws/diag");
                endpoints.MapHub<Hubs.GPIOHub>("/ws/gpio");
                //if (settings.EnableScheduler)
                //    endpoints.MapHangfireDashboard();
            });

            
            switch (settings.MQTTMode)
            {
                case MQTTMode.Disabled:
                    break;
                case MQTTMode.Server:
                    mqttServerService.Start();
                    break;
                case MQTTMode.Bridge:
                    mqttServerService.Start();
                    mqttClientService.Start();
                    break;
                case MQTTMode.Client:
                    mqttClientService.Start();
                    break;
                default:
                    break;
            }
            device.StartMonitoring();
        }


        void ConfigureWebSocketEndpoint(IApplicationBuilder application, MqttServerService mqttServerService, MqttSettingsModel mqttSettings, RaspberryDevice device)
        {
            if (mqttSettings?.WebSocketEndPoint?.Enabled != true)
            {
                enableMqqtWebSocket = false;
            }
            if (string.IsNullOrEmpty(mqttSettings.WebSocketEndPoint.Path))
            {
                enableMqqtWebSocket = false;
            }

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(mqttSettings.WebSocketEndPoint.KeepAliveInterval)
            };
            if (mqttSettings.WebSocketEndPoint.AllowedOrigins?.Any() == true)
            {
                foreach (var item in mqttSettings.WebSocketEndPoint.AllowedOrigins)
                {
                    webSocketOptions.AllowedOrigins.Add(item);
                }
            }
            application.UseWebSockets(webSocketOptions);

            application.Use(async (context, next) =>
            {
                if (enableMqqtWebSocket && context.Request.Path == mqttSettings.WebSocketEndPoint.Path)
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        string subProtocol = null;
                        if (context.Request.Headers.TryGetValue("Sec-WebSocket-Protocol", out var requestedSubProtocolValues))
                        {
                            subProtocol = MqttSubProtocolSelector.SelectSubProtocol(requestedSubProtocolValues);
                        }

                        using (var webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol).ConfigureAwait(false))
                        {
                            await mqttServerService.RunWebSocketConnectionAsync(webSocket, context).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    await next().ConfigureAwait(false);
                }
            });
        }

    }
}
