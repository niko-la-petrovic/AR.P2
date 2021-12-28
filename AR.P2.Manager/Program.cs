using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AR.P2.Manager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                //.UseSerilog((hostingContext, services, loggerConfiguration) =>
                //{
                //    loggerConfiguration
                //        .MinimumLevel.Information()
                //        .Enrich
                //        .FromLogContext()
                //        .WriteTo.Console();
                //}, writeToProviders: false)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
