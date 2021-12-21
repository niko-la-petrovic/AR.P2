using AR.P2.Manager.Configuration;
using AR.P2.Manager.Configuration.Settings;
using AR.P2.Manager.Configuration.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddFileUploadServices(Configuration);

            services.AddSwaggerGen(options =>
            {
                //Configuration.GetSection(SwaggerSettings.SectionName).Get< SwaggerDocSettings>    
                options.SwaggerDoc(SwaggerSettings.DocumentName, new Microsoft.OpenApi.Models.OpenApiInfo { Title = "test" });
            });
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            FileUploadSettings fileUploadSettings)
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

            app.UseStaticFiles();

            app.UseRouting();

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
            });
        }
    }
}
