using APBDWebApp.Services;
using Microsoft.AspNetCore.Mvc;


namespace APBDWebApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController(IDbService service) : ControllerBase
{
    
    /// GET /api/trips
    /// Retrieves all available trips along with their basic information and associated countries.
    [HttpGet]
    public async Task<IActionResult> GetAllTripsDetails()
    {
        return Ok(await service.GetTripsAsync());
    }
}