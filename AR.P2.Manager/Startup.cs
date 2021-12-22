using AR.P2.Manager.Configuration;
using AR.P2.Manager.Configuration.Settings;
using AR.P2.Manager.Configuration.Swagger;
using AR.P2.Manager.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Np.AspNetCore.Authorization.OpenApi.Swagger.Configuration;

namespace AR.P2.Manager
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            System.Console.WriteLine((configuration as IConfigurationRoot).GetDebugView());
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddMvc();
            services.AddControllers();

            services.AddDbServices(Configuration);
            services.AddMetrics();
            services.AddFileUploadServices(Configuration);

            services.AddSwaggerGen(options =>
            {
                var swaggerDocSettings = Configuration.GetSection(SwaggerSettings.SectionName).Get<SwaggerDocSettings>();

                options.SwaggerDoc(SwaggerSettings.DocumentName, swaggerDocSettings.OpenApiInfo);
            });

            long maxUploadBytes = (long)(4 * System.Math.Pow(10, 9));
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = maxUploadBytes;
            });
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = maxUploadBytes;
            });
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            FileUploadSettings fileUploadSettings,
            ApplicationDbContext dbContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseFileUpload(env, fileUploadSettings);

            dbContext.Database.EnsureCreated();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseMetrics();

            app.UseAuthorization();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint($"{SwaggerSettings.DocumentName}/swagger.json", SwaggerSettings.DocumentName);
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapSwagger();
                endpoints.MapPrometheusMetrics();
            });
        }
    }
}
