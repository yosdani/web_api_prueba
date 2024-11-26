using CommonTypes.Settings.App;
using CommonTypes.Settings;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using CommonTypes.Log;
using CommonTypes.Settings.App.AppSettingsItems;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using api_prueba.Auth;
using api_prueba.Support;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<IJwtAuthenticationService, JwtAuthenticationService>();

// Add services to the container.
#region AppSettings
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json");
#endregion
#region LoadBalancers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddControllers();
#endregion
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
#region APISettings
IConfigurationSection appSettingsConfig = builder.Configuration.GetSection(nameof(AppSettings));
builder.Services.Configure<AppSettings>(appSettingsConfig);
builder.Services.AddSingleton(resolver => resolver.GetRequiredService<IOptionsMonitor<AppSettings>>().CurrentValue);
IConfigurationSection dbKeysConfig = builder.Configuration.GetSection(nameof(DBKeysSettings));
builder.Services.Configure<DBKeysSettings>(dbKeysConfig);
builder.Services.AddSingleton(resolver => resolver.GetRequiredService<IOptionsMonitor<DBKeysSettings>>().CurrentValue);
#endregion

#region Log4Net
builder.Logging.ClearProviders();
//builder.Logging.AddLog4Net(appSettingsConfig.GetSection(nameof(Log4Net)).GetSection(nameof(Log4Net.Log4NetConfigFile)).Value);
builder.Services.AddSingleton(new LogWriter());
#endregion
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Prueba_Web_API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = $"JWT Authorization header using the Bearer scheme.{Environment.NewLine}{Environment.NewLine}Enter 'Bearer' [space] and then your token in the text input below.{Environment.NewLine}{Environment.NewLine}Example: \"Bearer 12345abcdef\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme
                                                                {
                                                                    Reference = new OpenApiReference
                                                                    {
                                                                        Type = ReferenceType.SecurityScheme,
                                                                        Id = "Bearer"
                                                                    }
                                                                }, new string[] { } } });
});
builder.Services.AddCors();
builder.Services.AddControllers();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
#region Security
string key = builder.Configuration.GetSection("APIKey").Value;

#endregion


builder.Services.AddAuthorization();

#region JwtAuthenticationService

builder.Services.AddSingleton<IJwtAuthenticationService>(new JwtAuthenticationService(key));
builder.Services.AddSingleton(new JwtAuthenticationService(key));
#endregion
builder.Services.AddDirectoryBrowser();

#region EnvironmentData
Tools.ContentRootPath = builder.Environment.ContentRootPath;
Tools.CurrentEnvironment = builder.Environment.EnvironmentName;
#endregion

app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
app.UseAuthentication();
app.UseAuthorization();

#region Config
Tools.Services = app.Services;
LogWriter.Configure(Tools.Settings.Log4Net.Log4NetConfigFile, null);
#endregion

#region DBSettings
Tools.ConfigureDBConnections();
#endregion

app.MapControllers();

app.Run();
