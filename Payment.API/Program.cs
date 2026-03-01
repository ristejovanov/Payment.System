using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.OpenApi.Models;
using Payment.API.Middleware;
using Security.DataServices.DependencyConfiguration;
using Serilog;
using System.Reflection;


const string AllowedCrossOrigins = "_allowedCrossOrigins";

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

#region 1️⃣ Host Configuration

if (WindowsServiceHelpers.IsWindowsService())
    Environment.CurrentDirectory = AppContext.BaseDirectory;

// Serilog configuration: read from appsettings.json
//serilog configuration
Log.Logger = new LoggerConfiguration().CreateLogger();
builder.Host.UseSerilog((hostContext, loggerConfiguration) =>
    _ = loggerConfiguration.ReadFrom.Configuration(builder.Configuration));


builder.Host.UseWindowsService();

#endregion

#region 2️⃣ Services Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowedCrossOrigins,
        corsBuilder =>
        {
            corsBuilder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithOrigins(builder.Configuration.GetSection("AllowedCrossOrigins").Get<string[]>() ?? new[] { "*" });
        });
});


// --- Dependency registration via extensions ---
builder.Services.InstallDependency();
builder.Services.AddMemoryCache();

// --- Controllers & JSON ---
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// --- Swagger ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Payment API", Version = "v1" });
});

#endregion

var app = builder.Build();


// After building, log application startup banner
var environment = app.Environment.EnvironmentName;
var hostName = Environment.MachineName;
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

Log.Information(
    "================================================================================\n" +
    "Payment API service started\n" +
    "Timestamp : {Timestamp}\n" +
    "Host       : {Host}\n" +
    "Environment: {Environment}\n" +
    "Version    : {Version}\n" +
    "================================================================================",
    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), hostName, environment, version);


#region 3️⃣ Pipeline Configuration

// --- Swagger UI (always available in dev/test) ---
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment API v1");
});


// because it is not a client app and just for test
//if (!app.Environment.IsDevelopment())
//{
//    app.UseHsts();
//    app.UseHttpsRedirection();
//}

app.UseRouting();
app.UseCors(AllowedCrossOrigins);


// --- Middlewares in professional order ---
app.Use(async (ctx, next) => { ctx.Request.EnableBuffering(); await next(); });
app.UseMiddleware<ExceptionHandlingMiddleware>();

// --- Routing ---
app.MapControllers();

#endregion

app.Run();
