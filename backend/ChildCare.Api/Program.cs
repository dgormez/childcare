using System.IdentityModel.Tokens.Jwt;
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
using ChildCare.Api.Auth;
using ChildCare.Api.Cli;
using ChildCare.Api.Endpoints;
using ChildCare.Api.Middleware;
using ChildCare.Api.Services;
using ChildCare.Application.Common;
using ChildCare.Application.Organisations;
using ChildCare.Application.Common.Behaviors;
using ChildCare.Application.ChildEvents;
using ChildCare.Application.Contracts;
using ChildCare.Application.ClosureCalendar;
using ChildCare.Application.RoomShifts;
using ChildCare.Infrastructure.Auth;
using ChildCare.Infrastructure.Concurrency;
using ChildCare.Infrastructure.Pdf;
using ChildCare.Infrastructure.Persistence;
using ChildCare.Infrastructure.Push;
using ChildCare.Infrastructure.Storage;
using FluentValidation;
using MediatR;
using QuestPDF.Infrastructure;

// migrate-tenants CLI subcommand (contracts/migrate-tenants-cli.md, research.md R8) — checked
// before the web host is built: it does not start the API, does not bind a port, and exits
// when done, so it must never fall through into the normal WebApplication startup below.
if (args.Length > 0 && args[0] == "migrate-tenants")
{
    var cliBuilder = Host.CreateApplicationBuilder(args);
    cliBuilder.Services.AddDbContext<PublicDbContext>(options =>
        options.UseNpgsql(cliBuilder.Configuration.GetConnectionString("DefaultConnection")));
    cliBuilder.Services.AddSingleton<ITenantDbContextResolver, TenantDbContextResolver>();

    using var cliHost = cliBuilder.Build();
    using var cliScope = cliHost.Services.CreateScope(); // PublicDbContext is Scoped
    var exitCode = await MigrateTenantsCommand.RunAsync(cliScope.ServiceProvider);
    Environment.Exit(exitCode);
}

// backfill-growth-check CLI subcommand (feature 009a-child-events-custom-type,
// contracts/child-events-api-delta.md, research.md R1/R2) — same early-exit shape as
// migrate-tenants above; MUST be run against every tenant schema before deploying a build
// whose ChildEventTypeExtensions no longer recognizes the literal "measurement" wire value.
if (args.Length > 0 && args[0] == "backfill-growth-check")
{
    var cliBuilder = Host.CreateApplicationBuilder(args);
    cliBuilder.Services.AddDbContext<PublicDbContext>(options =>
        options.UseNpgsql(cliBuilder.Configuration.GetConnectionString("DefaultConnection")));

    using var cliHost = cliBuilder.Build();
    using var cliScope = cliHost.Services.CreateScope(); // PublicDbContext is Scoped
    var exitCode = await BackfillGrowthCheckCommand.RunAsync(cliScope.ServiceProvider);
    Environment.Exit(exitCode);
}

// Raises the ThreadPool's minimum worker threads above the .NET default (= ProcessorCount)
// so a sudden burst of concurrent requests doesn't stall on the pool's slow "hill-climbing"
// thread-injection rate. Auth endpoints in particular call BCrypt.Verify/HashPassword
// synchronously (CPU-bound, deliberately expensive) inside otherwise-async handlers; under
// SC-001's "up to 50 concurrent authentication requests" on an 8-core machine, only
// ProcessorCount requests could run in parallel without this, pushing tail latency for the
// rest well past the 2-second budget while they wait for new threads to spin up — found by
// AuthMultiTenantLoginTests.Login_FiftyConcurrentRequests_EachCompletesWithinTwoSeconds
// (feature 003, /speckit-converge T068).
ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount, 100), Math.Max(Environment.ProcessorCount, 100));

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
// Skip Npgsql registration in Testing — integration tests inject InMemory/TestContainers via
// WebApplicationFactory.
if (!builder.Environment.IsEnvironment("Testing"))
{
    // Organisation onboarding (feature 001) — shared public schema (tenants, invitations).
    builder.Services.AddDbContext<PublicDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddScoped<IPublicDbContext>(sp => sp.GetRequiredService<PublicDbContext>());
}

// ── Multi-tenancy scaffold (feature 002) ────────────────────────────────────────
// CurrentTenantService/ICurrentTenantService MUST be Scoped, never Singleton — a singleton
// would leak one request's tenant into a concurrent request's (research.md R2).
builder.Services.AddScoped<CurrentTenantService>();
builder.Services.AddScoped<ICurrentTenantService>(sp => sp.GetRequiredService<CurrentTenantService>());

// Stateless besides IConfiguration — Singleton (mirrors TenantProvisioningService).
builder.Services.AddSingleton<ITenantDbContextResolver, TenantDbContextResolver>();

// Request-scoped ITenantDbContext, built via the resolver reading the current request's
// resolved schema. Only valid for non-exempt routes, once TenantMiddleware has run —
// ForSchema itself now throws if SchemaName isn't set yet, rather than silently building a
// context against the wrong schema (see TenantDbContextResolver.ForSchema).
builder.Services.AddScoped<ITenantDbContext>(sp =>
    sp.GetRequiredService<ITenantDbContextResolver>()
        .ForSchema(sp.GetRequiredService<ICurrentTenantService>().SchemaName));

// IMiddleware-typed, so it's resolvable both by the pipeline and by test code via
// factory.Services.GetRequiredService<TenantMiddleware>() (research.md R3).
builder.Services.AddSingleton<TenantMiddleware>();

// ── MediatR + FluentValidation (constitution Principle III) ────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<RegisterOrganisationCommand>();
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<RegisterOrganisationCommand>();

// ── Organisation onboarding services ────────────────────────────────────────────
// Singleton: stateless besides IConfiguration, and this lets tests resolve the exact
// instance a request will use to set FailureInjectionHookForTests (tasks.md T049).
builder.Services.AddSingleton<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();

// ── JWT Auth ──────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<EmailService>());
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IAppleTokenValidator, AppleTokenValidator>();
builder.Services.AddScoped<ChildCare.Application.Auth.OrganisationSlugResolver>();
builder.Services.AddHttpClient();

// ── Staff management (feature 005-staff) ────────────────────────────────────
// No IStaffDeactivationGuard registration — IEnumerable<IStaffDeactivationGuard> resolves
// empty until features 009/011 each register their own (research.md R4).
builder.Services.AddScoped<IProfilePhotoStorage, GcsProfilePhotoStorage>();
builder.Services.AddScoped<IHealthAttachmentStorage, GcsHealthAttachmentStorage>();

// ── Group activities (feature 009b) ─────────────────────────────────────────
builder.Services.AddScoped<IGroupActivityPhotoStorage, GcsGroupActivityPhotoStorage>();
builder.Services.AddScoped<ChildCare.Application.GroupActivities.GroupActivityMapper>();

// ── Caregiver kiosk mode (feature 008a) ─────────────────────────────────────
// Device-token issuance mirrors JwtService/JwtAccessTokenIssuer's existing pattern — a
// distinct signing key from the user-session JWT (research.md R1).
builder.Services.AddSingleton<DeviceTokenService>();
builder.Services.AddScoped<IDeviceTokenIssuer, DeviceTokenIssuer>();
// Plain injectable services (not MediatR commands) shared across check-in/check-out/
// confirm-administrator command handlers — mirrors how CloseStaleShiftsHelper/
// IShiftAttributionService are also called from within other commands' handlers, not from
// endpoints directly (plan.md Constitution Check).
builder.Services.AddScoped<ChildCare.Application.Staff.VerifyPinCommand>();
builder.Services.AddScoped<IShiftAttributionService, ShiftAttributionService>();
builder.Services.AddScoped<CloseStaleShiftsHelper>();

// ── Enrolment contracts (feature 007-contracts) ─────────────────────────────
// Additive registrations — join whatever else is already registered against
// IEnumerable<ILocationDeactivationGuard>/IEnumerable<IChildDeactivationGuard> (research.md R3).
builder.Services.AddScoped<IAdvisoryLockService, PostgresAdvisoryLockService>();
builder.Services.AddScoped<ILocationDeactivationGuard, ContractLocationDeactivationGuard>();
builder.Services.AddScoped<IChildDeactivationGuard, ContractChildDeactivationGuard>();
builder.Services.AddScoped<IContractPdfGenerator, QuestPdfContractGenerator>();
QuestPDF.Settings.License = LicenseType.Community;

// ── Child events (feature 009-child-events) ─────────────────────────────────
builder.Services.AddScoped<ITemperatureAlertService, TemperatureAlertService>();
builder.Services.AddScoped<IExpoPushSender, ExpoPushSender>();

// ── Attendance (feature 010-attendance) ─────────────────────────────────────
// Plain injectable service (not a MediatR command), called directly from CheckInCommand's
// handler — mirrors IShiftAttributionService/CloseStaleShiftsHelper's existing pattern.
builder.Services.AddScoped<ChildCare.Application.Attendance.PlannedDurationCalculator>();

// ── Incident reports (feature 013b) ─────────────────────────────────────────
builder.Services.AddScoped<IIncidentReportPdfGenerator, QuestPdfIncidentReportGenerator>();

// ── Closure calendar (feature 011) ─────────────────────────────────────────
builder.Services.AddScoped<ClosureParentRecipientResolver>();
builder.Services.AddScoped<ClosureNotificationService>();
builder.Services.AddScoped<ClosureAttendanceService>();
builder.Services.AddScoped<IClosureCalendarReader, ClosureCalendarReader>();

// ── Parent communication (feature 013) ──────────────────────────────────────
// Reuses IExpoPushSender (009) unchanged (research.md R3) — no new push registration needed.
builder.Services.AddScoped<ICurrentParentContactResolver, CurrentParentContactResolver>();

// ── Day reservations (feature 013a) ──────────────────────────────────────────
// Reuses IExpoPushSender/ICurrentParentContactResolver/IAdvisoryLockService/IClosureCalendarReader
// unchanged — no new ports needed (research.md).
builder.Services.AddScoped<ChildCare.Application.DayReservations.DayReservationNotificationService>();

// ── Reservation settings (feature 013f) ──────────────────────────────────────
builder.Services.AddScoped<ChildCare.Application.DayReservations.ReservationPolicyResolver>();

var deviceJwtSecret = builder.Configuration["DeviceJwt:Secret"]
    ?? throw new InvalidOperationException("DeviceJwt:Secret is not configured.");

builder.Services.AddAuthentication(options =>
{
    // Feature 008a (research.md R1): the actual default scheme is a policy scheme that
    // cheaply inspects the incoming token's issuer (decode-without-validate) and forwards to
    // either the ordinary user-JWT scheme or the DeviceToken scheme below — so
    // TenantMiddleware's existing context.User.FindFirst("tenant_id") code needs zero changes
    // regardless of which credential type authenticated the request.
    options.DefaultScheme          = "TokenSchemeForwarder";
    options.DefaultChallengeScheme = "TokenSchemeForwarder";
})
    .AddPolicyScheme("TokenSchemeForwarder", "TokenSchemeForwarder", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var rawToken = authHeader["Bearer ".Length..].Trim();
                try
                {
                    var issuer = new JwtSecurityTokenHandler().ReadJwtToken(rawToken).Issuer;
                    if (issuer == builder.Configuration["DeviceJwt:Issuer"])
                        return "DeviceToken";
                }
                catch (Exception)
                {
                    // Malformed token — fall through to the ordinary user-JWT scheme, which
                    // rejects it with the standard 401 rather than this forwarder guessing.
                }
            }
            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
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
    })
    // Feature 008a (kiosk mode, research.md R1): a second, distinctly-keyed JWT bearer scheme
    // for paired-tablet device tokens. OnTokenValidated checks the revocation list and
    // token_version on every request, not only at issuance (FR-021) — this runs before any
    // endpoint/command code, which is what structurally guarantees FR-029's device-token-
    // before-PIN precedence and FR-030's revocation-beats-rotation precedence (research.md R3's
    // rotation filter never even runs for a request this event has already failed). No
    // ICurrentTenantService/ITenantDbContext is available yet at this point in the pipeline
    // (TenantMiddleware runs after UseAuthentication), so the schema is resolved directly from
    // the token's own tenant_id claim, mirroring LoginCommandHandler's exempt-route pattern.
    .AddJwtBearer("DeviceToken", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["DeviceJwt:Issuer"],
            ValidAudience            = builder.Configuration["DeviceJwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(deviceJwtSecret)),
            ClockSkew                = TimeSpan.Zero,
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var tenantIdClaim      = context.Principal?.FindFirst(DeviceTokenClaims.TenantId)?.Value;
                var deviceIdClaim      = context.Principal?.FindFirst(DeviceTokenClaims.DeviceId)?.Value;
                var tokenVersionClaim  = context.Principal?.FindFirst(DeviceTokenClaims.TokenVersion)?.Value;

                if (!Guid.TryParse(tenantIdClaim, out var tenantId) ||
                    !Guid.TryParse(deviceIdClaim, out var deviceId) ||
                    !int.TryParse(tokenVersionClaim, out var tokenVersion))
                {
                    context.Fail("Malformed device token claims.");
                    return;
                }

                var publicDb = context.HttpContext.RequestServices.GetRequiredService<PublicDbContext>();
                var tenant = await publicDb.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant is null)
                {
                    context.Fail("Unknown tenant for device token.");
                    return;
                }

                var tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantDbContextResolver>();
                var db = tenantResolver.ForSchema(tenant.SchemaName);
                var pairing = await db.DevicePairings.FirstOrDefaultAsync(d => d.Id == deviceId);

                if (pairing is null || pairing.RevokedAt is not null)
                {
                    context.HttpContext.Items["DeviceTokenRejectReason"] = "errors.devices.revoked";
                    // FR-021: every rejected request from a revoked device is audit-logged with
                    // the same rigor as CorrectShiftCommand's shift-correction logging — this
                    // is also what proves a since-revoked offline-queue replay is logged on sync,
                    // since sync requests hit this same auth pipeline before any endpoint code.
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ChildCare.Api.DeviceTokenAuth")
                        .LogWarning(
                            "Rejected request from revoked or unknown device {DeviceId} (tenant {TenantId}) at {Path}",
                            deviceId, tenantId, context.HttpContext.Request.Path);
                    context.Fail("Device has been revoked.");
                    return;
                }

                // US6/FR-020: accepts the current version *or* the one immediately before it —
                // a one-generation grace window. DeviceTokenRotationFilter increments
                // TokenVersion the moment a token nears expiry, but an offline-queue replay
                // burst can still be mid-flight on the pre-rotation token when that happens
                // (research.md R3's whole rationale for gating rotation at all — "the first
                // replayed request would invalidate the token the remaining queued requests
                // still carry"). Strict equality here would reject requests 2..N of that same
                // burst the instant request 1 rotates. A token two or more rotations behind
                // (tokenVersion < TokenVersion - 1) is still naturally rejected, so this remains
                // "no separate revocation-list entry per rotation" (data-model.md) for anything
                // beyond one grace generation.
                if (tokenVersion < pairing.TokenVersion - 1)
                {
                    context.HttpContext.Items["DeviceTokenRejectReason"] = "errors.devices.token_expired";
                    context.Fail("Device token has been superseded by a rotation.");
                    return;
                }
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                var errorKey = context.HttpContext.Items["DeviceTokenRejectReason"] as string
                    ?? "errors.devices.token_expired";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsJsonAsync(new { errorKey });
            },
        };
    })
    // Phase 1 super-admin gate (research.md R11) — a distinct, non-default scheme so it never
    // competes with JWT bearer auth on the rest of the app; opted into per-endpoint via the
    // "SuperAdmin" policy below.
    .AddScheme<SuperAdminAuthenticationOptions, SuperAdminAuthenticationHandler>(
        SuperAdminAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy => policy
        .AddAuthenticationSchemes(SuperAdminAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser());

    // Role-based access control (feature 003, research.md R5) — RequireRole reads the
    // standard ClaimTypes.Role claim (JwtService.GenerateAccessToken) and already fails
    // closed (missing/unrecognized role claim never grants access) and returns 403, not 401,
    // for an authenticated-but-unauthorized request (FR-014).
    options.AddPolicy("DirectorOnly",    policy => policy.RequireRole("director"));
    options.AddPolicy("StaffOrDirector", policy => policy.RequireRole("staff", "director"));
    options.AddPolicy("ParentOnly",      policy => policy.RequireRole("parent"));

    // Feature 008a (kiosk mode) — a paired tablet's device token, not a user role. Named its
    // own scheme explicitly (mirrors "SuperAdmin" above) rather than relying on the default
    // forwarder, since these routes must reject a valid *user* JWT too (a caregiver's/
    // director's own session token is not a device token).
    options.AddPolicy("DeviceAuthenticated", policy => policy
        .AddAuthenticationSchemes("DeviceToken")
        .RequireAuthenticatedUser());

    // Feature 009 — PATCH/DELETE /api/child-events/{id} accept either a paired tablet's device
    // token (caregiver, same-day-and-location only — checked in the handler via
    // ChildEventEditWindowPolicy) or a director's user JWT (any event, any day). ASP.NET Core
    // tries each listed scheme and succeeds if either authenticates (research.md R4,
    // contracts/child-events-api.md — no existing endpoint needed this combination before).
    options.AddPolicy("DeviceOrDirector", policy => policy
        .AddAuthenticationSchemes("DeviceToken", JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());

    // Feature 009c (research.md R2) — GET /api/children and GET /api/groups predate 008a's
    // kiosk/device-token model (they were built StaffOrDirector-only in feature 008) and a
    // paired kiosk tablet's device token carries no role claim, so a plain RequireRole would
    // reject it. A device token grants access on its own (no role needed, mirrors
    // DeviceAuthenticated above); a user JWT still needs the staff/director role (mirrors
    // StaffOrDirector above) — RequireAssertion rather than RequireRole since the two accepted
    // schemes need different rules, not one rule ANDed across both.
    options.AddPolicy("DeviceOrStaffOrDirector", policy => policy
        .AddAuthenticationSchemes("DeviceToken", JwtBearerDefaults.AuthenticationScheme)
        .RequireAssertion(context =>
            context.User.HasClaim(c => c.Type == DeviceTokenClaims.DeviceId) ||
            context.User.IsInRole("staff") ||
            context.User.IsInRole("director")));
});

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
        document.Components.SecuritySchemes["SuperAdminKey"] = new OpenApiSecurityScheme
        {
            Name        = SuperAdminAuthenticationHandler.HeaderName,
            Type        = SecuritySchemeType.ApiKey,
            In          = ParameterLocation.Header,
            Description = "Temporary Phase 1 super-admin key (research.md R11).",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, ct) =>
    {
        var authData = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().ToList();
        var schemeName = authData.Count > 0 && authData.Any(a => a.Policy == "SuperAdmin")
            ? "SuperAdminKey"
            : authData.Count > 0 ? "Bearer" : null;

        if (schemeName is not null)
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(schemeName)] = []
            }];
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ── Auto-migrate on startup (dev only) ───────────────────────────────────────
// For production: run `dotnet ef database update` manually or via release pipeline
// (constitution Principle VI — the "new tenant schema" carve-out does not extend to this
// shared public schema, which behaves like any other shared/existing-data schema).
using (var scope = app.Services.CreateScope())
{
    // Optional: existing test factories don't necessarily register PublicDbContext via Npgsql
    // — this feature's own factories migrate it explicitly in their own setup instead of
    // relying on this dev-only block.
    var publicDb = scope.ServiceProvider.GetService<PublicDbContext>();
    var env      = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    if (env.IsDevelopment())
        publicDb?.Database.Migrate();
}

// ── Middleware ────────────────────────────────────────────────────────────────

// Global exception handler — logs full error server-side, returns generic 500 to client.
// FluentValidation.ValidationException is handled here too (rather than duplicated as a
// try/catch in every MediatR-backed endpoint) since every validated command is guaranteed to
// throw exactly this type via the shared ValidationBehavior pipeline (constitution Principle III).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var feature = context.Features.Get<IExceptionHandlerFeature>();

        // errorKey, not raw text — constitution Principle IV (NON-NEGOTIABLE): every error
        // response uses a locale-aware key, so the client resolves the message, not the server.
        if (feature?.Error is ValidationException validationEx)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            var fieldErrors = validationEx.Errors.ToDictionary(e => e.PropertyName, e => e.ErrorMessage);
            await context.Response.WriteAsJsonAsync(new { errorKey = "errors.validation", fieldErrors });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        if (feature?.Error is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(feature.Error, "Unhandled exception");
        }

        await context.Response.WriteAsJsonAsync(new { errorKey = "errors.unexpected" });
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

// Deny-by-default tenant resolution (FR-015) — after auth, so the tenant_id claim is
// available; every non-[TenantExempt] route below is scoped by this (research.md R3).
app.UseMiddleware<TenantMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Dev-only API docs — not tenant domain data, so exempt (research.md R3); without this,
    // TenantMiddleware rejects anonymous access to /openapi/*.json and /scalar/v1 with a 401.
    app.MapOpenApi().RequireTenantExempt();
    app.MapScalarApiReference().RequireTenantExempt();  // UI at /scalar/v1
}

// ── Health check ──────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithTags("Health")
   .RequireTenantExempt();

// ── Feature endpoints ─────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapOrganisationEndpoints();
app.MapLocationEndpoints();
app.MapStaffEndpoints();
app.MapChildrenEndpoints();
app.MapContactsEndpoints();
app.MapGroupsEndpoints();
app.MapContractsEndpoints();
app.MapDevicePairingEndpoints();
app.MapRoomShiftEndpoints();
app.MapChildEventEndpoints();
app.MapGroupActivityEndpoints();
app.MapAttendanceEndpoints();
app.MapClosureCalendarEndpoints();
app.MapStaffScheduleEndpoints();
app.MapWaitingListEndpoints();
app.MapParentInvitationEndpoints();
app.MapMessageThreadEndpoints();
app.MapAnnouncementEndpoints();
app.MapNotificationEndpoints();
app.MapParentEndpoints();
app.MapDayReservationEndpoints();
app.MapIncidentReportEndpoints();
app.MapVaccineRecordEndpoints();
app.MapHealthRecordEndpoints();
app.MapMealListEndpoints();

// Test-only role-policy endpoints (feature 003, research.md R5) — never mapped outside the
// integration test host.
if (app.Environment.IsEnvironment("Testing"))
    app.MapTestSupportEndpoints();

// E2E seeding support (Playwright, web/e2e) — never mapped outside a local dev server; see
// Endpoints/E2ESupportEndpoints.cs's doc comment for why this needs to exist at all.
if (app.Environment.IsDevelopment())
    app.MapE2ESupportEndpoints();

app.Run();

public partial class Program { }
