using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnimarket.Api.Models.Dtos.Pedidos.Checkout;
using Omnimarket.Api.Services;
using Omnimarket.Api.Utils;

namespace Omnimarket.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/checkout")]
    public class CheckoutController : ControllerBase
    {
        private readonly CheckoutService _checkoutService;

        public CheckoutController(CheckoutService checkoutService)
        {
            _checkoutService = checkoutService;
        }

        [HttpGet("preparacao")]
        public async Task<IActionResult> PrepararCheckout([FromQuery] int? enderecoId)
        {
            var usuarioId = User.GetUserId();
            var resultado = await _checkoutService.PrepararCheckoutAsync(usuarioId, enderecoId);
            return Ok(resultado);
        }

        [HttpPost]
        public async Task<IActionResult> FinalizarCheckout([FromBody] CheckoutCriacaoDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var usuarioId = User.GetUserId();
                var resultado = await _checkoutService.FinalizarCheckoutAsync(usuarioId, dto);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }
    }
}
