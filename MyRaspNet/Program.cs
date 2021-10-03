
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyRaspNet
{
    public class Program
    {
        private static CancellationTokenSource cancelTokenSource = new System.Threading.CancellationTokenSource();
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.RunAsync(cancelTokenSource.Token).GetAwaiter().GetResult();
        }
        private static async void StartAsync(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync(cancelTokenSource.Token);
        }
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)

                  .ConfigureLogging(logging =>
                  {
                      logging.ClearProviders();
                      logging.AddConsole();
                      logging.AddDebug();
                      //logging.AddEventLog();
                      //logging.AddFile("Logs/myapp-{Date}.txt");
                  })
                  .ConfigureWebHostDefaults(webBuilder =>
                  {
                      webBuilder.ConfigureKestrel(serverOptions =>
                      {
                      });
                      webBuilder.UseStartup<Startup>();
                  });
                  //.ConfigureAppConfiguration((hostingContext, config) =>
                  //{
                  //    config.SetBasePath(Directory.GetCurrentDirectory());
                  //    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                  //    config.AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
                  //    config.AddEnvironmentVariables();
                  //});
            return host;
        }
        public static void Shutdown()
        {
            cancelTokenSource.Cancel();
        }
    }
}
