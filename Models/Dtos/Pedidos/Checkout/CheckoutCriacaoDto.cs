using System.ComponentModel.DataAnnotations;
using Omnimarket.Api.Models.Dtos.Pedidos.ItemPedido;

namespace Omnimarket.Api.Models.Dtos.Pedidos.Checkout
{
    public class CheckoutCriacaoDto
    {
        public int? EnderecoId { get; set; }

        [StringLength(500)]
        public string Observacao { get; set; } = string.Empty;

        [MinLength(1, ErrorMessage = "Informe ao menos uma loja para finalizar o checkout.")]
        public List<CheckoutLojaCriacaoDto> Lojas { get; set; } = new();
    }

    public class CheckoutLojaCriacaoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Loja invalida.")]
        public int LojaId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Opcao de entrega invalida.")]
        public int LojaEntregaOpcaoId { get; set; }

        [StringLength(500)]
        public string? Observacao { get; set; }

        [MinLength(1, ErrorMessage = "Informe ao menos um item para a loja.")]
        public List<ItemPedidoDto> Itens { get; set; } = new();
    }
}
