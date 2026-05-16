using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PetitionService.Server.Data;
using PetitionService.Server.Storage;
using PetitionService.Server.AI;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using System.IO.Compression;
using System.Threading.RateLimiting;

namespace PetitionService.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
        builder.Services.AddHttpClient<IGeminiPetitionAssistant, GeminiPetitionAssistant>();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("gemini-assist", limiterOptions =>
            {
                limiterOptions.PermitLimit = 5;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });
        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Fastest;
        });

        // EF Core SQLite
        builder.Services.AddDbContext<PetitionService.Server.Data.AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=petitions.db"));

        // Identity
        builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredLength =6;
        }).AddEntityFrameworkStores<PetitionService.Server.Data.AppDbContext>()
        .AddDefaultTokenProviders();

        // JWT Auth
        var jwtKey = builder.Configuration["Jwt:Key"] ?? "DEV_KEY_CHANGE_ME_123456789_ABCDEF_123456789";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrWhiteSpace(context.Token)
                        && context.Request.Cookies.TryGetValue("accessToken", out var token)
                        && !string.IsNullOrWhiteSpace(token))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
        });
        builder.Services.AddAuthorization();

        var objectStorageProvider = builder.Configuration["ObjectStorage:Provider"] ?? "Local";
        if (string.Equals(objectStorageProvider, "Minio", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IObjectStorage, MinioObjectStorage>();
        }
        else
        {
            builder.Services.AddScoped<IObjectStorage, LocalObjectStorage>();
        }

        var app = builder.Build();

        // Apply migrations / create db
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Database.GetMigrations().Any())
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            EnsureRoleExists(roleManager, "User").GetAwaiter().GetResult();
            EnsureRoleExists(roleManager, "Admin").GetAwaiter().GetResult();

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PetitionAttachments (
                    Id INTEGER NOT NULL CONSTRAINT PK_PetitionAttachments PRIMARY KEY AUTOINCREMENT,
                    PetitionId INTEGER NOT NULL,
                    StorageKey TEXT NOT NULL,
                    OriginalFileName TEXT NOT NULL,
                    ContentType TEXT NOT NULL,
                    SizeBytes INTEGER NOT NULL,
                    UploadedBy TEXT NOT NULL,
                    UploadedAt TEXT NOT NULL,
                    CONSTRAINT FK_PetitionAttachments_Petitions_PetitionId FOREIGN KEY (PetitionId) REFERENCES Petitions (Id) ON DELETE CASCADE
                );
            ");

            db.Database.ExecuteSqlRaw(@"
                CREATE INDEX IF NOT EXISTS IX_PetitionAttachments_PetitionId
                ON PetitionAttachments (PetitionId);
            ");
        }

        app.MapDefaultEndpoints();

        app.UseResponseCompression();
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var fileName = context.File.Name;
                var isVersionedAsset = fileName.Contains("-", StringComparison.Ordinal)
                    && (fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                        || fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase));

                context.Context.Response.Headers.CacheControl = isVersionedAsset
                    ? "public,max-age=31536000,immutable"
                    : "public,max-age=3600";
            }
        });

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseStatusCodePages(async context =>
        {
            var httpContext = context.HttpContext;
            var path = httpContext.Request.Path;

            if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var statusCode = httpContext.Response.StatusCode;
            if (statusCode is not (StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound
                or StatusCodes.Status410Gone))
            {
                return;
            }

            httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
            var payload = JsonSerializer.Serialize(new
            {
                type = $"https://httpstatuses.com/{statusCode}",
                title = statusCode switch
                {
                    StatusCodes.Status403Forbidden => "Forbidden",
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status410Gone => "Gone",
                    _ => "HTTP Error"
                },
                status = statusCode,
                traceId = httpContext.TraceIdentifier
            });

            await httpContext.Response.WriteAsync(payload);
        });


        app.MapControllers();

        app.MapGet("/robots.txt", (HttpContext httpContext) =>
        {
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var lines = new[]
            {
                "User-agent: *",
                "Allow: /",
                "Disallow: /api/",
                "Disallow: /auth/",
                "Disallow: /swagger/",
                "Disallow: /login",
                "",
                $"Sitemap: {baseUrl}/sitemap.xml"
            };

            return Results.Text(string.Join("\n", lines), "text/plain; charset=utf-8");
        });

        app.MapGet("/sitemap.xml", (HttpContext httpContext) =>
        {
            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var xml = $"""
<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">
  <url>
    <loc>{baseUrl}/</loc>
    <lastmod>{now}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.8</priority>
  </url>
</urlset>
""";

            return Results.Text(xml, "application/xml; charset=utf-8");
        });

        app.MapGet("/", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
        });

        app.MapGet("/auth/login", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
        });

        app.MapGet("/petitions", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath!, "index.html"));
        });

        app.MapFallback((HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });

        app.Run();
    }

    private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
