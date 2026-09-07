using Auth.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route(".well-known")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class WellKnownController : ControllerBase
{
    private readonly RsaSigningKeyStore _signingKeys;

    public WellKnownController(RsaSigningKeyStore signingKeys)
    {
        _signingKeys = signingKeys;
    }

    [HttpGet("jwks.json")]
    [ProducesResponseType<JsonWebKeySet>(StatusCodes.Status200OK)]
    public ActionResult<JsonWebKeySet> Get()
    {
        return Ok(JsonWebKeySet.Create(_signingKeys));
    }
}
