using System.Text;
using Cap1.LogiTrack;
using Cap1.LogiTrack.Models;
using Cap1.LogiTrack.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddDbContext<LogiTrackContext>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<LogiTrackContext>();

builder.Services.Configure<JwtOption>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddMemoryCache();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    var config = configuration.GetSection("Jwt").Get<JwtOption>() 
                    ?? throw new ArgumentNullException(nameof(JwtOption));
    var issuer = config.Issuer ?? throw new ArgumentNullException(nameof(config.Issuer));
    var audience = config.Audience ?? throw new ArgumentNullException(nameof(config.Audience));
    var secretKey = config.Key ?? throw new ArgumentNullException(nameof(config.Key));
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidIssuer = issuer,
        ValidAudience = audience,
        ClockSkew = TimeSpan.Zero
    };


    // Log why authentication failed
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtBearer");
            logger.LogError(context.Exception, "JWT authentication failed");
            return Task.CompletedTask;
        }
    };
});


// Register cache service (can be easily switched to distributed cache)
builder.Services.AddScoped<Cap1.LogiTrack.Services.ICacheService, Cap1.LogiTrack.Services.MemoryCacheService>();

// Add Response Caching
builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 1024 * 1024; // 1MB
    options.UseCaseSensitivePaths = false;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable response caching middleware
app.UseResponseCaching();

app.MapControllers();
app.Run();
