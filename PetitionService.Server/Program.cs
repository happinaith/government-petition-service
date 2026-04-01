using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PetitionService.Server.Data;
using System.Security.Claims;

namespace PetitionService.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // EF Core SQLite
        builder.Services.AddDbContext<PetitionService.Server.Data.AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        var jwtKey = builder.Configuration["Jwt:Key"] ?? "DEV_KEY_CHANGE_ME_123456789";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"];
        var jwtAudience = builder.Configuration["Jwt:Audience"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrWhiteSpace(context.Token) &&
                        context.Request.Cookies.TryGetValue("accessToken", out var accessToken) &&
                        !string.IsNullOrWhiteSpace(accessToken))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var tokenVersionClaim = context.Principal?.FindFirst("tv")?.Value;

                    if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(tokenVersionClaim, out var tokenVersion))
                    {
                        context.Fail("Invalid token claims.");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var userState = await db.UserSecurityStates.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.UserId == userId);

                    // If state is missing (legacy users), accept only version 1 tokens.
                    var currentVersion = userState?.TokenVersion ?? 1;
                    if (tokenVersion != currentVersion)
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            };
        });
        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Apply migrations / create db
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<AppDbContext>();

            // Создаём БД и таблицы, если их ещё нет, но НЕ удаляем при каждом запуске
            db.Database.EnsureCreated();

            // Для существующих БД без миграций: создаём auth-таблицы, если их ещё нет.
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS UserSecurityStates (
                    UserId TEXT NOT NULL PRIMARY KEY,
                    TokenVersion INTEGER NOT NULL DEFAULT 1,
                    UpdatedAt TEXT NOT NULL
                );
            ");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS RefreshTokens (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    TokenHash TEXT NOT NULL,
                    ExpiresAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    RevokedAt TEXT NULL,
                    ReplacedByTokenHash TEXT NULL,
                    RevokedReason TEXT NULL,
                    TokenVersion INTEGER NOT NULL
                );
            ");

            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_RefreshTokens_TokenHash ON RefreshTokens(TokenHash);");
            db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_RefreshTokens_UserId_ExpiresAt ON RefreshTokens(UserId, ExpiresAt);");

            // Инициализация стандартных ролей
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            if (!roleManager.Roles.Any(r => r.Name == "Admin"))
            {
                roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
            }

            if (!roleManager.Roles.Any(r => r.Name == "User"))
            {
                roleManager.CreateAsync(new IdentityRole("User")).GetAwaiter().GetResult();
            }

            // Если есть другая инициализация — оставь её здесь
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();


        app.MapControllers();

        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
