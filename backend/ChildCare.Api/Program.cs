using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using ChildCare.Api.Data;
using ChildCare.Api.Endpoints;
using ChildCare.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
// Skip Npgsql registration in Testing — integration tests inject InMemory via WebApplicationFactory.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// ── JWT Auth ──────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<StripeService>();

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeSecretKey))
    Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
builder.Services.AddHttpClient();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero,  // no grace period after expiry
        };
    });

builder.Services.AddAuthorization();

// ── Rate limiting ─────────────────────────────────────────────────────────────
if (!builder.Environment.IsEnvironment("Testing") && !builder.Environment.IsDevelopment())
builder.Services.AddRateLimiter(options =>
{
    // Strict: login + register — brute-force targets
    options.AddPolicy("auth-strict", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(15),
                SegmentsPerWindow    = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    // Generous: Google OAuth — not a brute-force risk, needs room for normal flows
    options.AddPolicy("auth-oauth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 30,
                Window               = TimeSpan.FromMinutes(15),
                SegmentsPerWindow    = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    // Token refresh: IP-based (no user context yet), prevents hammering stolen tokens
    options.AddPolicy("auth-refresh", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 20,
                Window               = TimeSpan.FromMinutes(15),
                SegmentsPerWindow    = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    // General authenticated API: per user, generous enough for normal use
    options.AddPolicy("api-user", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 200,
                Window               = TimeSpan.FromMinutes(1),
                SegmentsPerWindow    = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    // Payment session creation: low limit per user — no legitimate reason to create many sessions
    options.AddPolicy("api-payment", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anon",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(15),
                SegmentsPerWindow    = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many attempts. Please try again later." }, token);
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────────
// Set Cors:AllowedOrigins in appsettings (or environment variables) for each environment.
// Mobile (React Native/Expo) and server-to-server calls are not subject to CORS;
// this only affects browser clients hitting the API directly.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length == 0)
            // No origins configured: allow any (dev convenience, not for production)
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        else
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
    });
});

// ── OpenAPI / Scalar ──────────────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info ??= new();
        document.Info.Title = "ChildCare API";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Name        = "Authorization",
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            In          = ParameterLocation.Header,
            Description = "Enter your JWT access token.",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = []
            }];
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ── Auto-migrate on startup (dev only) ───────────────────────────────────────
// For production (Azure): run `dotnet ef database update` manually or via release pipeline.
using (var scope = app.Services.CreateScope())
{
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    if (env.IsDevelopment()) db.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────────

// Global exception handler — logs full error server-side, returns generic 500 to client.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(feature.Error, "Unhandled exception");
        }

        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again." });
    });
});

// Security headers — Cloud Run / Container Apps terminate TLS before the app,
// so HSTS is only useful here in environments where the app itself serves HTTPS.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"]         = "DENY";
    context.Response.Headers["X-XSS-Protection"]        = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
    if (!app.Environment.IsDevelopment())
        context.Response.Headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";
    await next();
});

app.UseCors();
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment()) app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();  // UI at /scalar/v1
}

// ── Health check ──────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithTags("Health");

// ── Feature endpoints ─────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapHabitEndpoints();
app.MapNotificationEndpoints();
app.MapPaymentEndpoints();

app.Run();

public partial class Program { }
