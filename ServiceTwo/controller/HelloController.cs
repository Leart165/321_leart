using Microsoft.AspNetCore.Mvc;
using workflow.DTOs;

namespace workflow.Controllers;

[ApiController]
[Route("[controller]")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public ActionResult<HelloDto> GetWorld()
    {
        var hello = new HelloDto();
        return Ok(hello);
    }
}