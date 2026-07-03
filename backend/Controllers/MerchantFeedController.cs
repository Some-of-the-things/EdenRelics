using System.Text;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
public class MerchantFeedController(IMerchantFeedService feed) : ControllerBase
{
    [HttpGet("api/merchant-feed.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetFeed()
    {
        string xml = await feed.BuildFeedXmlAsync();
        return Content(xml, "application/xml", Encoding.UTF8);
    }
}
