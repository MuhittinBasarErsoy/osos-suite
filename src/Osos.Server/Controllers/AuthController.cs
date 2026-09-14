using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Osos.Contracts;
using Osos.Server.Data;
using Osos.Server.Services;

namespace Osos.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly TokenService _tokens;

    public AuthController(UserManager<AppUser> users, TokenService tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var user = new AppUser { UserName = req.Username, Email = req.Email };
        var result = await _users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        var (token, exp) = _tokens.Create(user);
        return new AuthResponse(token, exp, user.UserName!);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(AppLoginRequest req)
    {
        var user = await _users.FindByNameAsync(req.Username);
        if (user is null || !await _users.CheckPasswordAsync(user, req.Password))
            return Unauthorized(new { message = "Kullanıcı adı veya şifre hatalı." });

        var (token, exp) = _tokens.Create(user);
        return new AuthResponse(token, exp, user.UserName!);
    }
}
