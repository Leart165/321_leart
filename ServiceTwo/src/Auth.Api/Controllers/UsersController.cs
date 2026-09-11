using Auth.Api.Dtos;
using Auth.Application.Auth;
using Auth.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("v1/users")]
[Produces("application/json")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly AuthService _auth;

    public UsersController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        User user = await _auth.GetAsync(userId, cancellationToken);
        return Ok(UserDto.From(user));
    }
}
