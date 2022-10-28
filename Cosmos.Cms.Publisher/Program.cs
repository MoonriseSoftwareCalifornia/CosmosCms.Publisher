using AspNetCore.Identity.CosmosDb.Extensions;
using Cosmos.Cms.Common.Data;
using Cosmos.Cms.Common.Data.Logic;
using Cosmos.Cms.Common.Services.Configurations;
using Newtonsoft.Json.Serialization;
using Jering.Javascript.NodeJS;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


var appInsightsConfig = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
builder.Services.AddApplicationInsightsTelemetry();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("ApplicationDbContextConnection");
// Name of the Cosmos database to use
var cosmosIdentityDbName = builder.Configuration.GetValue<string>("CosmosIdentityDbName");

//
// Add the Cosmos database context here
//
builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseCosmos(connectionString: connectionString, databaseName: cosmosIdentityDbName));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddMvc()
                .AddNewtonsoftJson(options =>
                    options.SerializerSettings.ContractResolver =
                        new DefaultContractResolver());

//
// Add Cosmos Identity here
//
builder.Services.AddCosmosIdentity<ApplicationDbContext, IdentityUser, IdentityRole>(
      options => options.SignIn.RequireConfirmedAccount = true
    )
    .AddDefaultUI() // Use this if Identity Scaffolding added
    .AddDefaultTokenProviders();

// https://docs.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-3.1&tabs=visual-studio
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "CosmosAuthCookie";
    o.ExpireTimeSpan = TimeSpan.FromDays(5);
    o.SlidingExpiration = true;
});

//
// Get the boot variables loaded, and
// do some validation to make sure Cosmos can boot up
// based on the values given.
//
var cosmosStartup = new CosmosStartup(builder.Configuration);

// Add Cosmos Options
var option = cosmosStartup.Build();
builder.Services.AddSingleton(option);
builder.Services.AddTransient<ArticleLogic>();

builder.Services.AddControllersWithViews();

// Add Node Services
builder.Services.AddNodeJS();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Deep path
app.UseEndpoints(
    endpoints =>
    {
        endpoints.MapControllerRoute(
            "MsValidation",
            ".well-known/microsoft-identity-association.json",
            new { controller = "Home", action = "GetMicrosoftIdentityAssociation" });

        endpoints.MapControllerRoute(
            "MyArea",
            "{area:exists}/{controller=Home}/{action=Index}/{id?}");

        endpoints.MapFallbackToController("Index", "Home");
    }
);

app.MapRazorPages();

app.Run();
