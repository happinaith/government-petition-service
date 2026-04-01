using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PetitionService.Server.Data;
using PetitionService.Server.Models;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
	private readonly UserManager<IdentityUser> _userManager;
	private readonly SignInManager<IdentityUser> _signInManager;
	private readonly IConfiguration _config;
	private readonly AppDbContext _db;

	public AuthController(
		UserManager<IdentityUser> userManager,
		SignInManager<IdentityUser> signInManager,
		IConfiguration config,
		AppDbContext db)
	{
		_userManager = userManager;
		_signInManager = signInManager;
		_config = config;
		_db = db;
	}

	public record RegisterRequest(string Username, string Password);
	public record LoginRequest(string Username, string Password);
	public record LogoutRequest(bool AllDevices = false);
	public record AuthResponse(
		string Username,
		bool IsAdmin);
	public record SetAdminRequest(string Username, bool IsAdmin);

	private int AccessTokenMinutes => _config.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 15;
	private int RefreshTokenDays => _config.GetValue<int?>("Jwt:RefreshTokenDays") ?? 30;
	private bool RevokeTokensOnLogin => _config.GetValue<bool?>("Jwt:RevokeTokensOnLogin") ?? true;

	[HttpPost("register")]
	public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
	{
		var user = new IdentityUser { UserName = req.Username };
		var createResult = await _userManager.CreateAsync(user, req.Password);
		if (!createResult.Succeeded) return BadRequest(createResult.Errors);

		var roleResult = await _userManager.AddToRoleAsync(user, "User");
		if (!roleResult.Succeeded) return BadRequest(roleResult.Errors);

		var tokenVersion = await EnsureUserSecurityState(user.Id);
		return await IssueTokens(user, tokenVersion);
	}

	/// <summary>
	/// Назначить или снять роль администратора у пользователя.
	/// Доступно только администраторам.
	/// </summary>
	[HttpPost("set-admin")]
	[Authorize(Roles = "Admin")]
	public async Task<IActionResult> SetAdmin([FromBody] SetAdminRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Username))
		{
			return BadRequest("Username is required");
		}

		var user = await _userManager.FindByNameAsync(request.Username);
		if (user == null)
		{
			return NotFound("User not found");
		}

		var isInRole = await _userManager.IsInRoleAsync(user, "Admin");

		IdentityResult result;
		if (request.IsAdmin && !isInRole)
		{
			result = await _userManager.AddToRoleAsync(user, "Admin");
		}
		else if (!request.IsAdmin && isInRole)
		{
			result = await _userManager.RemoveFromRoleAsync(user, "Admin");
		}
		else
		{
			return Ok(new { username = request.Username, isAdmin = isInRole });
		}

		if (!result.Succeeded)
		{
			return BadRequest(result.Errors);
		}

		var updatedIsAdmin = await _userManager.IsInRoleAsync(user, "Admin");
		return Ok(new { username = request.Username, isAdmin = updatedIsAdmin });
	}

	[HttpPost("login")]
	public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
	{
		var user = await _userManager.FindByNameAsync(req.Username);
		if (user is null) return Unauthorized();

		var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
		if (!passwordCheck.Succeeded) return Unauthorized();

		var tokenVersion = await EnsureUserSecurityState(user.Id);
		if (RevokeTokensOnLogin)
		{
			tokenVersion = await RevokeAllRefreshTokensAndBumpVersion(user.Id, "Re-login");
		}

		return await IssueTokens(user, tokenVersion);
	}

	[HttpPost("refresh")]
	[AllowAnonymous]
	public async Task<ActionResult<AuthResponse>> Refresh()
	{
		if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
		{
			DeleteAuthCookies();
			return Unauthorized();
		}

		var refreshTokenHash = ComputeSha256(refreshToken);
		var storedToken = await _db.RefreshTokens
			.FirstOrDefaultAsync(x => x.TokenHash == refreshTokenHash);

		if (storedToken is null)
		{
			DeleteAuthCookies();
			return Unauthorized();
		}

		if (!storedToken.IsActive)
		{
			await HandleInactiveRefreshToken(storedToken);
			DeleteAuthCookies();
			return Unauthorized();
		}

		var user = await _userManager.FindByIdAsync(storedToken.UserId);
		if (user is null)
		{
			DeleteAuthCookies();
			return Unauthorized();
		}

		var tokenVersion = await EnsureUserSecurityState(user.Id);
		if (storedToken.TokenVersion != tokenVersion)
		{
			storedToken.RevokedAt = DateTime.UtcNow;
			storedToken.RevokedReason = "Outdated token version";
			await _db.SaveChangesAsync();
			DeleteAuthCookies();
			return Unauthorized();
		}

		storedToken.RevokedAt = DateTime.UtcNow;
		storedToken.RevokedReason = "Rotated";

		var authResponse = await IssueTokens(user, tokenVersion);
		if (!Request.Cookies.TryGetValue("refreshToken", out var newRefreshToken) || string.IsNullOrWhiteSpace(newRefreshToken))
		{
			DeleteAuthCookies();
			return Unauthorized();
		}

		storedToken.ReplacedByTokenHash = ComputeSha256(newRefreshToken);
		await _db.SaveChangesAsync();

		return authResponse;
	}

	[Authorize]
	[HttpPost("logout")]
	public async Task<IActionResult> Logout([FromBody] LogoutRequest? request)
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrWhiteSpace(userId))
		{
			return Unauthorized();
		}

		if (request?.AllDevices == true)
		{
			await RevokeAllRefreshTokensAndBumpVersion(userId, "Logout all devices");
			DeleteAuthCookies();
			return NoContent();
		}

		await RevokeCurrentRefreshToken(userId, "Logout current session");
		DeleteAuthCookies();
		return NoContent();
	}

	[Authorize]
	[HttpPost("compromised")]
	public async Task<IActionResult> Compromised()
	{
		var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
		if (string.IsNullOrWhiteSpace(userId))
		{
			return Unauthorized();
		}

		await RevokeAllRefreshTokensAndBumpVersion(userId, "Compromised account");
		DeleteAuthCookies();
		return NoContent();
	}

	[Authorize]
	[HttpGet("me")]
	public ActionResult<object> Me()
	{
		return Ok(new
		{
			Username = User.Identity?.Name,
			IsAdmin = User.IsInRole("Admin")
		});
	}

	private async Task<AuthResponse> IssueTokens(IdentityUser user, int tokenVersion)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, user.Id),
			new(ClaimTypes.Name, user.UserName ?? string.Empty),
			new("tv", tokenVersion.ToString())
		};

		var roles = await _userManager.GetRolesAsync(user);

		// Если у пользователя ещё нет ролей (старые аккаунты),
		// автоматически даём базовую роль User.
		if (roles.Count == 0)
		{
			var addUserRoleResult = await _userManager.AddToRoleAsync(user, "User");
			if (addUserRoleResult.Succeeded)
			{
				roles = await _userManager.GetRolesAsync(user);
			}
		}

		foreach (var role in roles)
		{
			claims.Add(new Claim(ClaimTypes.Role, role));
		}

		var now = DateTime.UtcNow;
		var accessTokenExpiresAt = now.AddMinutes(AccessTokenMinutes);
		var refreshTokenExpiresAt = now.AddDays(RefreshTokenDays);

		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
		var jwt = new JwtSecurityToken(
			issuer: _config["Jwt:Issuer"],
			audience: _config["Jwt:Audience"],
			claims: claims,
			expires: accessTokenExpiresAt,
			signingCredentials: creds);

		var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
		var rawRefreshToken = GenerateSecureToken();
        SetAuthCookies(accessToken, accessTokenExpiresAt, rawRefreshToken, refreshTokenExpiresAt);

		_db.RefreshTokens.Add(new RefreshToken
		{
			UserId = user.Id,
			TokenHash = ComputeSha256(rawRefreshToken),
			CreatedAt = now,
			ExpiresAt = refreshTokenExpiresAt,
			TokenVersion = tokenVersion
		});

		await _db.SaveChangesAsync();

		var isAdmin = roles.Contains("Admin");
		return new AuthResponse(user.UserName!, isAdmin);
	}

	private async Task<int> EnsureUserSecurityState(string userId)
	{
		var state = await _db.UserSecurityStates.FirstOrDefaultAsync(x => x.UserId == userId);
		if (state is not null)
		{
			return state.TokenVersion;
		}

		state = new UserSecurityState
		{
			UserId = userId,
			TokenVersion = 1,
			UpdatedAt = DateTime.UtcNow
		};

		_db.UserSecurityStates.Add(state);
		await _db.SaveChangesAsync();
		return state.TokenVersion;
	}

	private async Task<int> RevokeAllRefreshTokensAndBumpVersion(string userId, string reason)
	{
		var state = await _db.UserSecurityStates.FirstOrDefaultAsync(x => x.UserId == userId);
		if (state is null)
		{
			state = new UserSecurityState
			{
				UserId = userId,
				TokenVersion = 1,
				UpdatedAt = DateTime.UtcNow
			};
			_db.UserSecurityStates.Add(state);
		}

		var now = DateTime.UtcNow;
		var activeTokens = await _db.RefreshTokens
			.Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > now)
			.ToListAsync();

		foreach (var token in activeTokens)
		{
			token.RevokedAt = now;
			token.RevokedReason = reason;
		}

		state.TokenVersion += 1;
		state.UpdatedAt = now;

		await _db.SaveChangesAsync();
		return state.TokenVersion;
	}

	private async Task RevokeCurrentRefreshToken(string userId, string reason)
	{
		if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
		{
			return;
		}

		var refreshTokenHash = ComputeSha256(refreshToken);
		var storedToken = await _db.RefreshTokens
			.FirstOrDefaultAsync(x => x.TokenHash == refreshTokenHash && x.UserId == userId);

		if (storedToken is null || storedToken.RevokedAt is not null)
		{
			return;
		}

		storedToken.RevokedAt = DateTime.UtcNow;
		storedToken.RevokedReason = reason;
		await _db.SaveChangesAsync();
	}

	private async Task HandleInactiveRefreshToken(RefreshToken token)
	{
		if (token.RevokedAt is not null && !string.IsNullOrWhiteSpace(token.ReplacedByTokenHash))
		{
			await RevokeAllRefreshTokensAndBumpVersion(token.UserId, "Refresh token reuse detected");
			return;
		}

		if (token.RevokedAt is null && token.ExpiresAt <= DateTime.UtcNow)
		{
			token.RevokedAt = DateTime.UtcNow;
			token.RevokedReason = "Expired";
			await _db.SaveChangesAsync();
		}
	}

	private static string GenerateSecureToken()
	{
		return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
	}

	private void SetAuthCookies(string accessToken, DateTime accessTokenExpiresAt, string refreshToken, DateTime refreshTokenExpiresAt)
	{
		var secure = Request.IsHttps;
		var sameSite = SameSiteMode.Lax;

		Response.Cookies.Append("accessToken", accessToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = secure,
			SameSite = sameSite,
			Expires = accessTokenExpiresAt,
			Path = "/"
		});

		Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = secure,
			SameSite = sameSite,
			Expires = refreshTokenExpiresAt,
			Path = "/api/auth"
		});
	}

	private void DeleteAuthCookies()
	{
		Response.Cookies.Delete("accessToken", new CookieOptions { Path = "/" });
		Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" });
	}

	private static string ComputeSha256(string value)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(hash);
	}
}
