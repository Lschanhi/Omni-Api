using Omnimarket.Api.Models.Dtos.Pedidos.Carrinho;
using Omnimarket.Api.Models.Dtos.Produtos.Lojas.Entregas;

namespace Omnimarket.Api.Models.Dtos.Pedidos.Checkout
{
    public class CheckoutPreparacaoLeituraDto
    {
        public int? EnderecoId { get; set; }
        public string? CepEntrega { get; set; }
        public string? CidadeEntrega { get; set; }
        public string? UfEntrega { get; set; }
        public int TotalItens { get; set; }
        public decimal ValorTotalProdutos { get; set; }
        public List<CheckoutLojaPreparacaoDto> Lojas { get; set; } = new();
    }

    public class CheckoutLojaPreparacaoDto
    {
        public int LojaId { get; set; }
        public string NomeLoja { get; set; } = string.Empty;
        public int TotalItens { get; set; }
        public decimal ValorTotalProdutos { get; set; }
        public List<CarrinhoItemLeituraDto> Itens { get; set; } = new();
        public List<LojaEntregaOpcaoLeituraDto> OpcoesEntrega { get; set; } = new();
    }
}
