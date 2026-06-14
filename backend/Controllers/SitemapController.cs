using System.Text;
using Eden_Relics_BE.Services;
using Microsoft.AspNetCore.Mvc;

namespace Eden_Relics_BE.Controllers;

[ApiController]
public class SitemapController(ISitemapService sitemap) : ControllerBase
{
    [HttpGet("api/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetSitemap()
    {
        string xml = await sitemap.BuildSitemapXmlAsync();
        return Content(xml, "application/xml", Encoding.UTF8);
    }
}
