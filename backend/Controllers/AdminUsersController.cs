using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController(IAdminUserService adminUsers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        return Ok(await adminUsers.GetAllAsync());
    }
}
