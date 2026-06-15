using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AccountsController(IAccountsService accounts) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AccountsSummaryDto>> GetSummary()
    {
        return Ok(await accounts.GetSummaryAsync());
    }
}
