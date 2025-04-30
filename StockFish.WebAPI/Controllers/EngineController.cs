using Microsoft.AspNetCore.Mvc;
using StockFish.WebAPI.Controllers.Base;

namespace StockFish.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EngineController : BaseApiController
    {
        //[HttpGet("best-move")]
        //[ProducesResponseType(typeof(BestMoveDTO), 200)]
        //[OpenApiOperation("get best move", "")]
        //[AllowAnonymous]
        /*public async Task<IActionResult> GetBestMove([FromQuery] GetBestMoveRequest request)
        {
            var responseDTO = await Mediator.Send(request);

            return Ok(responseDTO);
        }*/
    }
}
