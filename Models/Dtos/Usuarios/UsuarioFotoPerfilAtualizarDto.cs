using System.ComponentModel.DataAnnotations;

namespace Omnimarket.Api.Models.Dtos.Usuarios
{
    public class UsuarioFotoPerfilAtualizarDto
    {
        [Required]
        public string DataUrl { get; set; } = string.Empty;

        [StringLength(260)]
        public string? NomeArquivo { get; set; }
    }
}
