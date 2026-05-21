using System.ComponentModel.DataAnnotations;

namespace Omnimarket.Api.Models.Dtos.Produtos
{
    public class ProdutoAtualizarDto
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "Preco deve ser maior que 0.")]
        public decimal? Preco { get; set; }

        [StringLength(1000)]
        public string? Descricao { get; set; }
    }
}
