using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnimarket.Api.Models.Dtos.Produtos.Lojas;
using Omnimarket.Api.Models.Enum;
using Omnimarket.Api.Services;
using Omnimarket.Api.Utils;

namespace Omnimarket.Api.Controllers
{
    [ApiController]
    [Route("api/lojas")]
    public class LojasController : ControllerBase
    {
        private readonly LojaService _lojaService;
        private readonly PedidoService _pedidoService;
        private readonly ReciboPedidoService _reciboPedidoService;

        public LojasController(
            LojaService lojaService,
            PedidoService pedidoService,
            ReciboPedidoService reciboPedidoService)
        {
            _lojaService = lojaService;
            _pedidoService = pedidoService;
            _reciboPedidoService = reciboPedidoService;
        }

        // Retorna a loja vinculada ao usuario autenticado.
        [Authorize]
        [HttpGet("minha")]
        public async Task<IActionResult> ObterMinhaLoja()
        {
            var usuarioId = User.GetUserId();
            var loja = await _lojaService.ObterMinhaLojaAsync(usuarioId);

            if (loja == null)
                return NotFound(new { mensagem = "Loja ainda nao cadastrada para este usuario." });

            return Ok(loja);
        }

        [Authorize]
        [HttpGet("minha/metricas")]
        public async Task<IActionResult> ObterMinhasMetricas()
        {
            var usuarioId = User.GetUserId();
            var metricas = await _lojaService.ObterMinhasMetricasAsync(usuarioId);

            if (metricas == null)
                return NotFound(new { mensagem = "Loja ainda nao cadastrada para este usuario." });

            return Ok(metricas);
        }

        [Authorize]
        [HttpGet("minha/pedidos")]
        public async Task<IActionResult> ListarPedidosDaMinhaLoja(
            [FromQuery] string? busca,
            [FromQuery] StatusPedido? statusPedido,
            [FromQuery] StatusVenda? statusVenda,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var usuarioId = User.GetUserId();
            var loja = await _lojaService.ObterMinhaLojaAsync(usuarioId);

            if (loja == null)
                return NotFound(new { mensagem = "Loja ainda nao cadastrada para este usuario." });

            var pedidos = await _pedidoService.ListarPedidosDaLojaAsync(
                loja.Id,
                usuarioId,
                busca,
                statusPedido,
                statusVenda,
                page,
                pageSize);

            return Ok(pedidos);
        }

        [Authorize]
        [HttpGet("minha/pedidos/{pedidoId:int}")]
        public async Task<IActionResult> BuscarPedidoDaMinhaLoja(int pedidoId)
        {
            var usuarioId = User.GetUserId();
            var loja = await _lojaService.ObterMinhaLojaAsync(usuarioId);

            if (loja == null)
                return NotFound(new { mensagem = "Loja ainda nao cadastrada para este usuario." });

            var pedido = await _pedidoService.BuscarPedidoDaLojaAsync(loja.Id, usuarioId, pedidoId);

            if (pedido == null)
                return NotFound(new { mensagem = "Pedido nao encontrado para a sua loja." });

            return Ok(pedido);
        }

        [Authorize]
        [HttpPut("minha/pedidos/{pedidoId:int}/status")]
        public async Task<IActionResult> AtualizarStatusPedidoDaMinhaLoja(
            int pedidoId,
            [FromBody] LojaAtualizarStatusPedidoDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var usuarioId = User.GetUserId();
            var loja = await _lojaService.ObterMinhaLojaAsync(usuarioId);

            if (loja == null)
                return NotFound(new { mensagem = "Loja ainda nao cadastrada para este usuario." });

            try
            {
                var pedido = await _pedidoService.AtualizarStatusPedidoDaLojaAsync(
                    loja.Id,
                    usuarioId,
                    pedidoId,
                    dto.StatusVenda);

                if (pedido == null)
                    return NotFound(new { mensagem = "Pedido nao encontrado para a sua loja." });

                var mensagem = dto.StatusVenda == StatusVenda.Cancelada
                    ? "Pedido cancelado com sucesso pela loja!"
                    : "Pedido marcado como enviado com sucesso pela loja!";

                return Ok(new
                {
                    mensagem,
                    pedido
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("minha/pedidos/{pedidoId:int}/recibo")]
        public async Task<IActionResult> BaixarReciboPedidoLoja(int pedidoId)
        {
            try
            {
                var usuarioId = User.GetUserId();
                var pdf = await _reciboPedidoService.GerarReciboPedidoParaVendedorAsync(pedidoId, usuarioId);

                if (pdf == null)
                    return NotFound(new { mensagem = "Pedido nao encontrado para a sua loja." });

                return File(
                    pdf,
                    "application/pdf",
                    $"recibo-pedido-{pedidoId}-loja.pdf");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        // Cria a loja do usuario autenticado.
        [Authorize]
        [HttpPost("minha")]
        public async Task<IActionResult> CriarMinhaLoja([FromBody] LojaCriacaoDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var usuarioId = User.GetUserId();
                var loja = await _lojaService.CriarMinhaLojaAsync(usuarioId, dto);

                return CreatedAtAction(nameof(ObterPorId), new { id = loja.Id }, loja);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        // Atualiza a loja do usuario autenticado.
        [Authorize]
        [HttpPut("minha")]
        public async Task<IActionResult> AtualizarMinhaLoja([FromBody] LojaAtualizacaoDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var usuarioId = User.GetUserId();
                var loja = await _lojaService.AtualizarMinhaLojaAsync(usuarioId, dto);

                if (loja == null)
                    return NotFound(new { mensagem = "Loja nao encontrada para este usuario." });

                return Ok(loja);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { mensagem = ex.Message });
            }
        }

        // Endpoint publico para consultar a loja pelo identificador.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObterPorId(int id)
        {
            var loja = await _lojaService.ObterPorIdAsync(id);

            if (loja == null)
                return NotFound(new { mensagem = "Loja nao encontrada." });

            return Ok(loja);
        }
    }
}
