using Omnimarket.Api.Models.Dtos.Pedidos.Carrinho;
using Omnimarket.Api.Models.Enum;

namespace Omnimarket.Api.Models.Dtos.Pedidos.Checkout
{
    public class CheckoutResultadoDto
    {
        public bool SucessoTotal { get; set; }
        public bool SucessoParcial { get; set; }
        public int QuantidadePedidosCriados { get; set; }
        public decimal ValorTotalProdutos { get; set; }
        public decimal ValorTotalFrete { get; set; }
        public decimal ValorTotalGeral { get; set; }
        public List<CheckoutPedidoCriadoDto> Pedidos { get; set; } = new();
        public List<CheckoutFalhaDto> Falhas { get; set; } = new();
        public CarrinhoLeituraDto CarrinhoAtualizado { get; set; } = new();
    }

    public class CheckoutPedidoCriadoDto
    {
        public int PedidoId { get; set; }
        public int LojaId { get; set; }
        public string NomeLoja { get; set; } = string.Empty;
        public int? LojaEntregaOpcaoId { get; set; }
        public string NomeEntrega { get; set; } = string.Empty;
        public int PrazoEntregaDias { get; set; }
        public int TotalItens { get; set; }
        public decimal ValorTotalProdutos { get; set; }
        public decimal ValorFrete { get; set; }
        public decimal ValorTotalPedido { get; set; }
        public StatusPedido Status { get; set; }
    }

    public class CheckoutFalhaDto
    {
        public int LojaId { get; set; }
        public string NomeLoja { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public List<int> ProdutoIds { get; set; } = new();
    }
}
