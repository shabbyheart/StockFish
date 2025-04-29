using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace StockFish.WebAPI.Controllers.Base
{
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        private ISender _mediator = null!;

        protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
    }
}
