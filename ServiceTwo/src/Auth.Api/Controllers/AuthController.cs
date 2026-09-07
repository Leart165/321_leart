using Auth.Api.Dtos;
using Auth.Application.Abstractions;
using Auth.Application.Auth;
using Auth.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("v1/auth")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        User user = await _auth.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);

        UserDto dto = UserDto.From(user);
        return CreatedAtAction(nameof(UsersController.GetAsync), "Users", new { userId = user.Id }, dto);
    }

    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        IssuedToken token = await _auth.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(new TokenResponse(token.AccessToken, "Bearer", token.ExpiresInSeconds));
    }
}
