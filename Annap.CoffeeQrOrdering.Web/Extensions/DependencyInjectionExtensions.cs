using Annap.CoffeeQrOrdering.Application;
using Microsoft.Extensions.Options;
using Annap.CoffeeQrOrdering.Infrastructure;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Hubs;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class DependencyInjectionExtensions
{
    public static WebApplicationBuilder AddAnnapWebServices(this WebApplicationBuilder builder, int devPort)
    {
        builder.Host.ConfigureHostOptions(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(15);
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10));
        }

        builder.Services.AddHttpContextAccessor();

        builder.Services.Configure<LanDemoOptions>(builder.Configuration.GetSection(LanDemoOptions.SectionName));
        builder.Services.Configure<AppUrlOptions>(builder.Configuration.GetSection(AppUrlOptions.SectionName));
        builder.Services.AddSingleton<ILanIpDetector, LanIpDetector>();
        builder.Services.AddSingleton<IPostConfigureOptions<AppUrlOptions>, AppUrlDevelopmentPostConfigure>();
        builder.Services.AddScoped<IAppUrlService, AppUrlService>();
        builder.Services.AddScoped<InfrastructureDiagnosticsService>();

        builder.Services.Configure<StaffAuthOptions>(builder.Configuration.GetSection(StaffAuthOptions.SectionName));
        builder.Services.Configure<DiagnosticsOptions>(
            builder.Configuration.GetSection(DiagnosticsOptions.SectionName));
        builder.Services.Configure<GuestOperationalOptions>(
            builder.Configuration.GetSection(GuestOperationalOptions.SectionName));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supported = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
            options.DefaultRequestCulture = new RequestCulture("vi-VN");
            options.SupportedCultures = supported;
            options.SupportedUICultures = supported;
            options.RequestCultureProviders =
            [
                new QueryStringRequestCultureProvider(),
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        builder.Services.AddSingleton<HubConnectionRegistry>();
        builder.Services.AddSingleton<IOrderStatusNotifier, OrderStatusNotifier>();
        builder.Services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = false;
            o.KeepAliveInterval = TimeSpan.FromSeconds(12);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
            o.MaximumReceiveMessageSize = 48 * 1024;
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/Staff/Login";
                o.AccessDeniedPath = "/Staff/Login";
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromHours(12);
                o.Cookie.Name = "annap_staff";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
            });
        builder.Services.AddAuthorization(o => o.AddPolicy("Staff", p => p.RequireRole("Staff")));

        builder.Services.AddRazorPages(o =>
        {
            o.Conventions.AuthorizeFolder("/Staff");
            o.Conventions.AuthorizeFolder("/Admin", "Staff");
            o.Conventions.AllowAnonymousToPage("/Diag/Mobile");
            o.Conventions.AllowAnonymousToPage("/Diag/Assets");
            o.Conventions.AllowAnonymousToPage("/Diag/Minimal");
            o.Conventions.AllowAnonymousToPage("/Staff/Login");
            o.Conventions.AllowAnonymousToPage("/Staff/Logout");
        });
        builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<GoLiveVerificationService>();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, _) =>
            {
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Too many requests. Please wait a moment and try again." },
                    cancellationToken: ctx.HttpContext.RequestAborted);
            };

            static string Partition(HttpContext http)
            {
                var xff = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(xff))
                {
                    var first = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(first))
                        return first;
                }

                return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }

            options.AddPolicy("anon-order-post", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    Partition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 45,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("anon-ai-post", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    Partition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 22,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<DrinkAssetResolver>();
        builder.Services.AddScoped<MenuMediaMaintenanceService>();

        return builder;
    }
}
