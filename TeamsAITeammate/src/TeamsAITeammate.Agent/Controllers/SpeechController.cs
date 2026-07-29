using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamsAITeammate.Core.Interfaces;

namespace TeamsAITeammate.Agent.Controllers;

[ApiController]
[Route("api/speech")]
[Authorize(AuthenticationSchemes = "TeamsTab")]
public class SpeechController : ControllerBase
{
    private readonly ISpeechTokenService _tokens;

    public SpeechController(ISpeechTokenService tokens)
    {
        _tokens = tokens;
    }

    [HttpGet("token")]
    public async Task<ActionResult<SpeechAuthorization>> GetToken(CancellationToken ct)
    {
        return Ok(await _tokens.GetAuthorizationAsync(ct));
    }
}