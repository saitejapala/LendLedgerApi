using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LendLedgerApi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LendLedgerApi.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _lookupService;

        public LookupsController(ILookupService lookupService)
        {
            _lookupService = lookupService;
        }

        [HttpGet]
        public ActionResult<Dictionary<string, List<object>>> GetLookups()
        {
            var allValues = _lookupService.GetAllValues();
            var result = allValues
                .GroupBy(v => v.Type)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(v => new { code = v.Code, value = v.Value, description = v.Description })
                          .ToList<object>()
                );

            return Ok(result);
        }

        [HttpPost("reload")]
        public async Task<IActionResult> Reload()
        {
            await _lookupService.ReloadAsync();
            return Ok(new { message = "Lookup cache reloaded successfully." });
        }
    }
}
