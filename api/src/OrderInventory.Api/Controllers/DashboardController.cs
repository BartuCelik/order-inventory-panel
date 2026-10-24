using Microsoft.AspNetCore.Mvc;
using OrderInventory.Application.Dashboard;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken cancellationToken) => dashboard.GetAsync(cancellationToken);
}
