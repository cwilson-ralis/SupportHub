namespace SupportHub.Web.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/email")]
public class EmailInboundController : ControllerBase
{
    [HttpPost("inbound")]
    [AllowAnonymous]
    public IActionResult InboundWebhook()
    {
        // Reserved for future Graph subscription push notifications (Phase 7+)
        return Ok();
    }
}
