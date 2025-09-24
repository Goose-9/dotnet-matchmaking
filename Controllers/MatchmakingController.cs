using Microsoft.AspNetCore.Mvc;

namespace MatchmakingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchmakingController : ControllerBase
{
    [HttpPost("join")]
    public IActionResult JoinQueue([FromBody] string playerId)
    {
        // Logic to add player to matchmaking queue
        return Ok(
            new
            {
                status = "Player added to queue",
                playerId = playerId,
                timestamp = DateTime.UtcNow,
            }
        );
    }

    [HttpGet("match")]
    public IActionResult GetMatch()
    {
        return Ok(new { status = "No match found yet." });
    }
}
