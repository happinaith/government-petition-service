using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace PetitionService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
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
 public record AuthResponse(string Token, string Username);

 [HttpPost("register")]
 public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
 {
 var user = new IdentityUser { UserName = req.Username };
 var result = await _userManager.CreateAsync(user, req.Password);
 if (!result.Succeeded) return BadRequest(result.Errors);
 return await IssueToken(user);
 }

 [HttpPost("login")]
 public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
 {
 var user = await _userManager.FindByNameAsync(req.Username);
 if (user is null) return Unauthorized();
 var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
 if (!passwordCheck.Succeeded) return Unauthorized();
 return await IssueToken(user);
 }

 [Authorize]
 [HttpGet("me")]
 public ActionResult<object> Me()
 {
 return Ok(new { Username = User.Identity?.Name });
 }

 private async Task<AuthResponse> IssueToken(IdentityUser user)
 {
 var claims = new List<Claim>
 {
 new(ClaimTypes.NameIdentifier, user.Id),
 new(ClaimTypes.Name, user.UserName ?? "")
 };
 var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
 var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
 var jwt = new JwtSecurityToken(
 issuer: _config["Jwt:Issuer"],
 audience: _config["Jwt:Audience"],
 claims: claims,
 expires: DateTime.UtcNow.AddHours(12),
 signingCredentials: creds);
 var token = new JwtSecurityTokenHandler().WriteToken(jwt);
 return new AuthResponse(token, user.UserName!);
 }
}
