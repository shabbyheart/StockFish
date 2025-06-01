using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockFish.WebAPI.Models;
using StockFish.WebAPI.Services.Stockfish;

namespace StockFish.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EngineController(StockfishRequestProcessor stockfishService) : ControllerBase
    {
        private readonly StockfishRequestProcessor _stockfishService = stockfishService;

        [HttpGet("best-move")]
        [ProducesResponseType(typeof(string), 200)]
        [AllowAnonymous]
        public async Task<IActionResult> GetBestMove([FromQuery] GetBestMoveDto request,CancellationToken ct)
        {
            var result = await _stockfishService.GetBestMoveAsync(request.FEN, request.BotLevel, ct);

            return Ok(result);
        }
    }
}
