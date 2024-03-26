using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Neo4j.Driver;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBP_I_PROJEKAT.Hubs;

namespace NBP_I_PROJEKAT
{
    public class Startup
    {
        public static IDriver driver;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddRazorPages();
            //services.AddControllers();
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            //services.AddControllersWithViews().AddRazorRuntimeCompilation()
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "NBP_I_PROJEKAT", Version = "v1" });
            });
            services.AddMvc();
            //services.AddControllersWithViews();
            services.AddSession(options => options.IdleTimeout = TimeSpan.FromDays(1));
            // Neo4j
            driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "17779"));
            services.AddSingleton(driver);
            services.AddRazorPages();
            // Redis
            services.AddSignalR().AddRedis("localhost:6379");
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true"));
        services.AddCors(c =>
{
    c.AddPolicy("AllowOrigin", options => options.AllowAnyOrigin());
});

        services.AddCors(options =>
      {
    options.AddPolicy("CorsPolicy",
        builder => builder
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithMethods("GET","PUT","DELETE","POST","PATCH") //not really necessary when AllowAnyMethods is used.
        );
          });
        }
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //if (env.IsDevelopment())
            //{
            //    app.UseDeveloperExceptionPage();
            //}
            //else
            //{
            //    app.UseExceptionHandler("/Error");
            //    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //    app.UseHsts();
            //}
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NBP_I_PROJEKAT v1"));
            }


            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseStaticFiles();
            app.UseAuthorization();
            app.UseCors(options => options.AllowAnyOrigin());

            app.UseSession();
            //app.UseCors("CORS");
            app.UseAuthentication();


            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapControllers();
                endpoints.MapControllerRoute(
                     name: "default",
                     pattern: "{controller=Home}/{action=Index}");
                //radi:Index,Login,Register,Profil kao radi,SacuvaneObjave ,
                //ne radi:KorisnikoveObjave,MojeObjave,ObjavaView ->verovatno zbog view sto nisam stavio 
                // Ovde moramo da dodamo hubove kasnije

                endpoints.MapHub<ChatHub>("/hub/Chat");
                endpoints.MapHub<ObjavaHub>("/hub/Objave");
                endpoints.MapRazorPages();
            });

        }
    }
}
