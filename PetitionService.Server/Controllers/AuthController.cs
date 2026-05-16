using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
 private const string AccessTokenCookieName = "accessToken";
 private const string RefreshTokenCookieName = "refreshToken";
 private const string RefreshTokenUserCookieName = "refreshTokenUser";
 private const string RefreshTokenProvider = "PetitionService";
 private const string RefreshTokenName = "refresh_token_hash";
 private const string RefreshTokenExpiryName = "refresh_token_expires";

 private readonly UserManager<IdentityUser> _userManager;
 private readonly SignInManager<IdentityUser> _signInManager;
 private readonly IConfiguration _config;

 public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IConfiguration config)
 {
 _userManager = userManager;
 _signInManager = signInManager;
 _config = config;
 }

 public record RegisterRequest(string Username, string Password);
 public record LoginRequest(string Username, string Password);
 public record GrantAdminRequest(string Username);
 public record BootstrapAdminRequest(string Username, string BootstrapToken);
 public record AuthResponse(string Username, string[] Roles, DateTime AccessTokenExpiresAtUtc);

 [HttpPost("register")]
 public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
 {
 var user = new IdentityUser { UserName = req.Username };
 var result = await _userManager.CreateAsync(user, req.Password);
 if (!result.Succeeded) return BadRequest(result.Errors);

 if (!await _userManager.IsInRoleAsync(user, "User"))
 {
 await _userManager.AddToRoleAsync(user, "User");
 }

 return await IssueTokensAsync(user);
 }

 [HttpPost("login")]
 public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
 {
 var user = await _userManager.FindByNameAsync(req.Username);
 if (user is null) return Unauthorized();
 var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
 if (!passwordCheck.Succeeded) return Unauthorized();
 return await IssueTokensAsync(user);
 }

 [HttpPost("refresh")]
 public async Task<ActionResult<AuthResponse>> Refresh()
 {
 var refreshToken = Request.Cookies[RefreshTokenCookieName];
 var refreshTokenUser = Request.Cookies[RefreshTokenUserCookieName];
 if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(refreshTokenUser))
 {
 ClearAuthCookies();
 return Unauthorized();
 }

 var user = await _userManager.FindByNameAsync(refreshTokenUser);
 if (user is null)
 {
 ClearAuthCookies();
 return Unauthorized();
 }

 var storedHash = await _userManager.GetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
 var storedExpiry = await _userManager.GetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName);

 if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedExpiry))
 {
 ClearAuthCookies();
 return Unauthorized();
 }

 if (!DateTime.TryParse(storedExpiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc)
 || expiresAtUtc <= DateTime.UtcNow)
 {
 await ClearRefreshTokenAsync(user);
 ClearAuthCookies();
 return Unauthorized();
 }

 var candidateHash = ComputeHash(refreshToken);
 if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(storedHash), Encoding.UTF8.GetBytes(candidateHash)))
 {
 await ClearRefreshTokenAsync(user);
 ClearAuthCookies();
 return Unauthorized();
 }

 return await IssueTokensAsync(user);
 }

 [Authorize]
 [HttpPost("logout")]
 public async Task<ActionResult> Logout()
 {
 var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

 var user = await _userManager.FindByIdAsync(userId);
 if (user is null) return Unauthorized();

 await ClearRefreshTokenAsync(user);
 ClearAuthCookies();
 return NoContent();
 }

 [Authorize(Roles = "Admin")]
 [HttpPost("grant-admin")]
 public async Task<ActionResult> GrantAdmin(GrantAdminRequest req)
 {
 var user = await _userManager.FindByNameAsync(req.Username);
 if (user is null) return NotFound(new { Message = "User not found" });

 if (!await _userManager.IsInRoleAsync(user, "Admin"))
 {
 await _userManager.AddToRoleAsync(user, "Admin");
 }

 if (!await _userManager.IsInRoleAsync(user, "User"))
 {
 await _userManager.AddToRoleAsync(user, "User");
 }

 return NoContent();
 }

 [HttpPost("bootstrap-admin")]
 public async Task<ActionResult> BootstrapAdmin(BootstrapAdminRequest req)
 {
 var configuredToken = _config["Auth:AdminBootstrapToken"];
 if (string.IsNullOrWhiteSpace(configuredToken))
 {
 return Problem("Admin bootstrap token is not configured", statusCode: 500);
 }

 if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configuredToken), Encoding.UTF8.GetBytes(req.BootstrapToken)))
 {
 return Unauthorized();
 }

 var currentAdmins = await _userManager.GetUsersInRoleAsync("Admin");
 if (currentAdmins.Count > 0)
 {
 return Conflict(new { Message = "Admin bootstrap is disabled because an admin already exists" });
 }

 var user = await _userManager.FindByNameAsync(req.Username);
 if (user is null) return NotFound(new { Message = "User not found" });

 await _userManager.AddToRoleAsync(user, "Admin");
 if (!await _userManager.IsInRoleAsync(user, "User"))
 {
 await _userManager.AddToRoleAsync(user, "User");
 }

 return NoContent();
 }

 [Authorize]
 [HttpGet("me")]
 public ActionResult<object> Me()
 {
 var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
 return Ok(new { Username = User.Identity?.Name, Roles = roles });
 }

 private async Task<AuthResponse> IssueTokensAsync(IdentityUser user)
 {
 var roles = await _userManager.GetRolesAsync(user);
 var claims = new List<Claim>
 {
 new(ClaimTypes.NameIdentifier, user.Id),
 new(ClaimTypes.Name, user.UserName ?? "")
 };

 claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

 var accessTokenTtlMinutes = _config.GetValue<int?>("Jwt:AccessTokenTtlMinutes") ?? 15;
 var accessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(accessTokenTtlMinutes);
 var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
 var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
 var jwt = new JwtSecurityToken(
 issuer: _config["Jwt:Issuer"],
 audience: _config["Jwt:Audience"],
 claims: claims,
 expires: accessTokenExpiresAtUtc,
 signingCredentials: creds);
 var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

 var refreshToken = GenerateRefreshToken();
 var refreshTokenHash = ComputeHash(refreshToken);
 var refreshTokenTtlDays = _config.GetValue<int?>("Jwt:RefreshTokenTtlDays") ?? 7;
 var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenTtlDays);

 await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName, refreshTokenHash);
 await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName, refreshTokenExpiresAtUtc.ToString("O"));

 SetAccessTokenCookie(accessToken, accessTokenExpiresAtUtc);
 SetRefreshTokenCookie(refreshToken, refreshTokenExpiresAtUtc);
 SetRefreshTokenUserCookie(user.UserName!, refreshTokenExpiresAtUtc);

 return new AuthResponse(user.UserName!, roles.ToArray(), accessTokenExpiresAtUtc);
 }

 private async Task ClearRefreshTokenAsync(IdentityUser user)
 {
 await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
 await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenExpiryName);
 }

 private static string GenerateRefreshToken()
 {
 return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
 }

 private static string ComputeHash(string value)
 {
 var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
 return Convert.ToHexString(bytes);
 }

 private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAtUtc)
 {
 Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
 {
 HttpOnly = true,
 Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
 SameSite = SameSiteMode.Strict,
 Expires = expiresAtUtc,
 IsEssential = true,
 Path = "/"
 });
 }

 private void SetAccessTokenCookie(string accessToken, DateTime expiresAtUtc)
 {
 Response.Cookies.Append(AccessTokenCookieName, accessToken, new CookieOptions
 {
 HttpOnly = true,
 Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
 SameSite = SameSiteMode.Strict,
 Expires = expiresAtUtc,
 IsEssential = true,
 Path = "/"
 });
 }

 private void SetRefreshTokenUserCookie(string username, DateTime expiresAtUtc)
 {
 Response.Cookies.Append(RefreshTokenUserCookieName, username, new CookieOptions
 {
 HttpOnly = true,
 Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
 SameSite = SameSiteMode.Strict,
 Expires = expiresAtUtc,
 IsEssential = true,
 Path = "/"
 });
 }

 private void ClearAuthCookies()
 {
 ClearCookie(AccessTokenCookieName);
 ClearCookie(RefreshTokenCookieName);
 ClearCookie(RefreshTokenUserCookieName);
 }

 private void ClearCookie(string cookieName)
 {
 Response.Cookies.Delete(cookieName, new CookieOptions
 {
 Path = "/",
 SameSite = SameSiteMode.Strict,
 Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
 HttpOnly = true
 });
 }
}
