using AR.P2.Manager.Configuration.Settings;
using AR.P2.Manager.Data;
using AR.P2.Manager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

namespace AR.P2.Manager.Configuration
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection AddDbServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

            return services;
        }

        public static IServiceCollection AddFileUploadServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(serviceProvider =>
            {
                return configuration.GetSection(FileUploadSettings.SectionName).Get<FileUploadSettings>();
            });

            services.AddScoped<IFileUploadService, FileUploadService>();

            return services;
        }

        public static IApplicationBuilder UseFileUpload(this IApplicationBuilder app, IWebHostEnvironment env, FileUploadSettings fileUploadSettings)
        {
            string fileUploadDirectoryPath = fileUploadSettings.UseWebRoot ?
                Path.Combine(env.WebRootPath, fileUploadSettings.FileUploadDirectoryPath) :
                fileUploadSettings.FileUploadDirectoryPath;

            PhysicalFileProvider fileProvider = new PhysicalFileProvider(fileUploadDirectoryPath);

            StaticFileOptions fileUploadOptions = new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = fileUploadSettings.FileUploadRequestPath
            };

            app.UseStaticFiles(fileUploadOptions);
            app.UseDirectoryBrowser(new DirectoryBrowserOptions
            {
                FileProvider = fileProvider,
                RequestPath = fileUploadSettings.FileUploadRequestPath
            });

            return app;
        }
    }
}
