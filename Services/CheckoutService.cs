using Microsoft.EntityFrameworkCore;
using Omni.Models.Entidades;
using Omnimarket.Api.Data;
using Omnimarket.Api.Models;
using Omnimarket.Api.Models.Dtos.Pedidos.Carrinho;
using Omnimarket.Api.Models.Dtos.Pedidos.Checkout;
using Omnimarket.Api.Models.Dtos.Pedidos.ItemPedido;
using Omnimarket.Api.Models.Entidades;
using Omnimarket.Api.Models.Enum;
using Omnimarket.Api.Utils;

namespace Omnimarket.Api.Services
{
    public class CheckoutService
    {
        private readonly DataContext _context;
        private readonly CarrinhoService _carrinhoService;
        private readonly LojaEntregaService _lojaEntregaService;
        private readonly PedidoService _pedidoService;

        public CheckoutService(
            DataContext context,
            CarrinhoService carrinhoService,
            LojaEntregaService lojaEntregaService,
            PedidoService pedidoService)
        {
            _context = context;
            _carrinhoService = carrinhoService;
            _lojaEntregaService = lojaEntregaService;
            _pedidoService = pedidoService;
        }

        public async Task<CheckoutPreparacaoLeituraDto> PrepararCheckoutAsync(int usuarioId, int? enderecoId)
        {
            var endereco = await ResolverEnderecoOpcionalAsync(usuarioId, enderecoId);
            var carrinho = await ObterCarrinhoCompletoAsync(usuarioId);

            if (carrinho == null || carrinho.Itens.Count == 0)
            {
                return new CheckoutPreparacaoLeituraDto
                {
                    EnderecoId = endereco?.Id,
                    CepEntrega = endereco?.Cep,
                    CidadeEntrega = endereco?.Cidade,
                    UfEntrega = endereco?.Uf
                };
            }

            var grupos = new List<CheckoutLojaPreparacaoDto>();

            foreach (var grupo in carrinho.Itens
                .OrderBy(i => i.Produto.Loja.NomeFantasia)
                .ThenBy(i => i.Produto.Nome)
                .GroupBy(i => new { i.Produto.LojaId, i.Produto.Loja.NomeFantasia }))
            {
                var itens = grupo
                    .Select(MapearItemCarrinho)
                    .ToList();

                var opcoesEntrega = await _lojaEntregaService.ListarOpcoesPublicasAsync(
                    grupo.Key.LojaId,
                    endereco?.Cep,
                    endereco?.Cidade,
                    endereco?.Uf);

                grupos.Add(new CheckoutLojaPreparacaoDto
                {
                    LojaId = grupo.Key.LojaId,
                    NomeLoja = grupo.Key.NomeFantasia,
                    TotalItens = itens.Sum(i => i.Quantidade),
                    ValorTotalProdutos = itens.Sum(i => i.Subtotal),
                    Itens = itens,
                    OpcoesEntrega = opcoesEntrega
                });
            }

            return new CheckoutPreparacaoLeituraDto
            {
                EnderecoId = endereco?.Id,
                CepEntrega = endereco?.Cep,
                CidadeEntrega = endereco?.Cidade,
                UfEntrega = endereco?.Uf,
                TotalItens = grupos.Sum(g => g.TotalItens),
                ValorTotalProdutos = grupos.Sum(g => g.ValorTotalProdutos),
                Lojas = grupos
            };
        }

        public async Task<CheckoutResultadoDto> FinalizarCheckoutAsync(int usuarioId, CheckoutCriacaoDto dto)
        {
            if (dto.Lojas == null || dto.Lojas.Count == 0)
                throw new InvalidOperationException("Informe ao menos uma loja para finalizar o checkout.");

            var carrinho = await ObterCarrinhoCompletoAsync(usuarioId);
            if (carrinho == null || carrinho.Itens.Count == 0)
                throw new InvalidOperationException("Carrinho vazio.");

            var carrinhoPorProduto = carrinho.Itens.ToDictionary(i => i.ProdutoId);
            var produtosProcessados = new HashSet<int>();
            var resultado = new CheckoutResultadoDto();

            foreach (var lojaDto in dto.Lojas)
            {
                var falhaValidacao = ValidarGrupoCheckout(
                    lojaDto,
                    carrinhoPorProduto,
                    produtosProcessados,
                    out var itensNormalizados,
                    out var nomeLoja);

                if (falhaValidacao != null)
                {
                    resultado.Falhas.Add(falhaValidacao);
                    continue;
                }

                try
                {
                    var pedido = await _pedidoService.CriarPedidoComEntregaDaLojaAsync(
                        usuarioId,
                        dto.EnderecoId,
                        lojaDto.LojaEntregaOpcaoId,
                        itensNormalizados,
                        CombinarObservacoes(dto.Observacao, lojaDto.Observacao));

                    resultado.Pedidos.Add(new CheckoutPedidoCriadoDto
                    {
                        PedidoId = pedido.Id,
                        LojaId = lojaDto.LojaId,
                        NomeLoja = pedido.Itens.FirstOrDefault()?.NomeLojaSnapshot ?? nomeLoja,
                        LojaEntregaOpcaoId = pedido.LojaEntregaOpcaoId,
                        NomeEntrega = string.IsNullOrWhiteSpace(pedido.NomeEntregaSnapshot)
                            ? EntregaHelper.ObterNomeTipoEntrega(pedido.TipoEntregaId)
                            : pedido.NomeEntregaSnapshot,
                        PrazoEntregaDias = pedido.PrazoEntregaDias,
                        TotalItens = pedido.Itens.Sum(i => i.Quantidade),
                        ValorTotalProdutos = pedido.ValorTotalProdutos,
                        ValorFrete = pedido.ValorFrete,
                        ValorTotalPedido = pedido.ValorTotalPedido,
                        Status = pedido.StatusPedidosId
                    });
                }
                catch (Exception ex)
                {
                    resultado.Falhas.Add(new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        NomeLoja = nomeLoja,
                        Mensagem = ex.Message,
                        ProdutoIds = itensNormalizados.Select(i => i.ProdutoId).ToList()
                    });
                }
            }

            resultado.CarrinhoAtualizado = await _carrinhoService.ObterCarrinhoAsync(usuarioId);
            resultado.QuantidadePedidosCriados = resultado.Pedidos.Count;
            resultado.ValorTotalProdutos = resultado.Pedidos.Sum(p => p.ValorTotalProdutos);
            resultado.ValorTotalFrete = resultado.Pedidos.Sum(p => p.ValorFrete);
            resultado.ValorTotalGeral = resultado.Pedidos.Sum(p => p.ValorTotalPedido);
            resultado.SucessoTotal = resultado.Pedidos.Count > 0 && resultado.Falhas.Count == 0;
            resultado.SucessoParcial = resultado.Pedidos.Count > 0 && resultado.Falhas.Count > 0;

            return resultado;
        }

        private async Task<Carrinho?> ObterCarrinhoCompletoAsync(int usuarioId)
        {
            return await _context.TBL_CARRINHO
                .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
                .ThenInclude(p => p.Midias)
                .Include(c => c.Itens)
                .ThenInclude(i => i.Produto)
                .ThenInclude(p => p.Loja)
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
        }

        private async Task<Endereco?> ResolverEnderecoOpcionalAsync(int usuarioId, int? enderecoId)
        {
            var query = _context.TBL_ENDERECO
                .AsNoTracking()
                .Where(e => e.UsuarioId == usuarioId && e.Ativo);

            if (enderecoId.HasValue && enderecoId.Value > 0)
                return await query.FirstOrDefaultAsync(e => e.Id == enderecoId.Value);

            return await query
                .OrderByDescending(e => e.IsPrincipal)
                .ThenBy(e => e.Id)
                .FirstOrDefaultAsync();
        }

        private static CarrinhoItemLeituraDto MapearItemCarrinho(ItemCarrinho item)
        {
            return new CarrinhoItemLeituraDto
            {
                Id = item.Id,
                ProdutoId = item.ProdutoId,
                Nome = item.Produto.Nome,
                Categoria = item.Produto.Categoria,
                LojaId = item.Produto.LojaId,
                NomeLoja = item.Produto.Loja.NomeFantasia,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.Produto.Preco,
                Subtotal = item.Produto.Preco * item.Quantidade,
                EstoqueDisponivel = item.Produto.Estoque,
                StatusPublicacao = item.Produto.StatusPublicacao,
                DisponivelParaCompra =
                    item.Produto.Loja.Ativa &&
                    item.Produto.StatusPublicacao == StatusProduto.Publicado &&
                    item.Produto.Estoque >= item.Quantidade,
                ImagemPrincipal = item.Produto.Midias
                    .OrderBy(m => m.Ordem)
                    .Select(m => m.Url)
                    .FirstOrDefault()
            };
        }

        private static CheckoutFalhaDto? ValidarGrupoCheckout(
            CheckoutLojaCriacaoDto lojaDto,
            IReadOnlyDictionary<int, ItemCarrinho> carrinhoPorProduto,
            ISet<int> produtosProcessados,
            out List<ItemPedidoDto> itensNormalizados,
            out string nomeLoja)
        {
            itensNormalizados = new List<ItemPedidoDto>();
            nomeLoja = string.Empty;

            if (lojaDto.Itens == null || lojaDto.Itens.Count == 0)
            {
                return new CheckoutFalhaDto
                {
                    LojaId = lojaDto.LojaId,
                    Mensagem = "Nenhum item foi informado para a loja."
                };
            }

            var itensAgrupados = lojaDto.Itens
                .GroupBy(i => i.ProdutoId)
                .Select(g => new ItemPedidoDto
                {
                    ProdutoId = g.Key,
                    Quantidade = g.Sum(x => x.Quantidade)
                })
                .ToList();

            var idsDoGrupo = new List<int>();

            foreach (var item in itensAgrupados)
            {
                idsDoGrupo.Add(item.ProdutoId);

                if (item.Quantidade <= 0)
                {
                    return new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        Mensagem = $"Quantidade invalida para o produto {item.ProdutoId}.",
                        ProdutoIds = idsDoGrupo
                    };
                }

                if (!carrinhoPorProduto.TryGetValue(item.ProdutoId, out var itemCarrinho))
                {
                    return new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        Mensagem = $"Produto {item.ProdutoId} nao esta mais no carrinho.",
                        ProdutoIds = idsDoGrupo
                    };
                }

                nomeLoja = itemCarrinho.Produto.Loja.NomeFantasia;

                if (itemCarrinho.Produto.LojaId != lojaDto.LojaId)
                {
                    return new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        NomeLoja = nomeLoja,
                        Mensagem = $"Produto {item.ProdutoId} nao pertence a loja informada.",
                        ProdutoIds = idsDoGrupo
                    };
                }

                if (produtosProcessados.Contains(item.ProdutoId))
                {
                    return new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        NomeLoja = nomeLoja,
                        Mensagem = $"Produto {item.ProdutoId} foi informado em mais de uma loja no checkout.",
                        ProdutoIds = idsDoGrupo
                    };
                }

                if (item.Quantidade > itemCarrinho.Quantidade)
                {
                    return new CheckoutFalhaDto
                    {
                        LojaId = lojaDto.LojaId,
                        NomeLoja = nomeLoja,
                        Mensagem = $"Quantidade solicitada para o produto {item.ProdutoId} excede o carrinho atual.",
                        ProdutoIds = idsDoGrupo
                    };
                }
            }

            foreach (var produtoId in idsDoGrupo)
                produtosProcessados.Add(produtoId);

            itensNormalizados = itensAgrupados;

            return null;
        }

        private static string CombinarObservacoes(string? observacaoGeral, string? observacaoLoja)
        {
            var partes = new[]
            {
                observacaoGeral?.Trim(),
                observacaoLoja?.Trim()
            }
            .Where(parte => !string.IsNullOrWhiteSpace(parte))
            .ToList();

            return string.Join(" | ", partes);
        }
    }
}
