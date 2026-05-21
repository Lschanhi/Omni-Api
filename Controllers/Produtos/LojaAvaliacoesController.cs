using Microsoft.AspNetCore.Mvc;
using Omnimarket.Api.Services;

namespace Omnimarket.Api.Controllers
{
    [ApiController]
    [Route("api/lojas/{lojaId:int}/avaliacoes")]
    public class LojaAvaliacoesController : ControllerBase
    {
        private readonly AvaliacaoProdutoService _service;

        public LojaAvaliacoesController(AvaliacaoProdutoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Listar(int lojaId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
            => Ok(await _service.ListarPorLojaAsync(lojaId, page, pageSize));
    }
}
