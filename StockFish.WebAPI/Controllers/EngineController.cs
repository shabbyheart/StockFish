using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFish.WebAPI.Controllers.Base;

namespace StockFish.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EngineController(IStockfishService stockfishService) : ControllerBase
    {
        private readonly IStockfishService _stockfishService = stockfishService;

        [HttpGet("best-move")]
        [ProducesResponseType(typeof(string), 200)]
        //[OpenApiOperation("get best move", "")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBestMove([FromQuery] GetBestMoveQuery query)
        {
            var result = await _stockfishService.GetBestMove(query);

            return Ok();
        }
    }
}
