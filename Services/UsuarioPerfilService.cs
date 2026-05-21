using Microsoft.EntityFrameworkCore;
using Omnimarket.Api.Data;
using Omnimarket.Api.Models;
using Omnimarket.Api.Models.Dtos.Usuarios;
using Omnimarket.Api.Models.Entidades;
using Omnimarket.Api.Utils;

namespace Omnimarket.Api.Services
{
    public class UsuarioPerfilService
    {
        private const int TamanhoMaximoFotoPerfilEmBytes = 2 * 1024 * 1024;
        private readonly DataContext _context;

        public UsuarioPerfilService(DataContext context)
        {
            _context = context;
        }

        public async Task<UsuarioPerfilLeituraDto?> ObterPerfilAsync(int usuarioId)
        {
            var usuario = await _context.TBL_USUARIO
                .Include(u => u.FotoPerfil)
                .Include(u => u.Telefones)
                .Include(u => u.Enderecos)
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario == null)
                return null;

            return new UsuarioPerfilLeituraDto
            {
                Id = usuario.Id,
                Cpf = usuario.Cpf,
                Nome = usuario.Nome,
                Sobrenome = usuario.Sobrenome,
                Email = usuario.Email,
                Role = usuario.Role,
                AvatarUrl = usuario.FotoPerfil == null ? null : MontarDataUrl(usuario.FotoPerfil),
                Telefones = usuario.Telefones
                    .Select(t => new UsuarioPerfilTelefoneLeituraDto
                    {
                        Id = t.Id,
                        NumeroE164 = t.NumeroE164,
                        IsPrincipal = t.IsPrincipal
                    })
                    .ToList(),
                Enderecos = usuario.Enderecos
                    .Where(e => e.Ativo)
                    .OrderByDescending(e => e.IsPrincipal)
                    .ThenBy(e => e.Id)
                    .Select(e => new UsuarioPerfilEnderecoLeituraDto
                    {
                        Id = e.Id,
                        TipoLogradouro = e.TipoLogradouro,
                        NomeEndereco = e.NomeEndereco,
                        Numero = e.Numero,
                        Cep = e.Cep,
                        Cidade = e.Cidade,
                        Uf = e.Uf,
                        IsPrincipal = e.IsPrincipal,
                        Ativo = e.Ativo
                    })
                    .ToList()
            };
        }

        public async Task<UsuarioFotoPerfilLeituraDto?> ObterFotoPerfilAsync(int usuarioId)
        {
            var fotoPerfil = await _context.TBL_USUARIO_FOTO_PERFIL
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UsuarioId == usuarioId);

            return fotoPerfil == null ? null : MapearFotoPerfil(fotoPerfil);
        }

        public async Task<UsuarioFotoPerfilLeituraDto> AtualizarFotoPerfilAsync(
            int usuarioId,
            UsuarioFotoPerfilAtualizarDto dto)
        {
            var usuarioExiste = await _context.TBL_USUARIO.AnyAsync(u => u.Id == usuarioId);
            if (!usuarioExiste)
                throw new InvalidOperationException("Usuario nao encontrado.");

            var (mimeType, conteudo) = ConverterDataUrl(dto.DataUrl);
            if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Envie uma imagem valida para a foto de perfil.");

            if (conteudo.Length > TamanhoMaximoFotoPerfilEmBytes)
                throw new InvalidOperationException("A foto de perfil deve ter no maximo 2 MB.");

            var nomeArquivo = SanitizarNomeArquivo(dto.NomeArquivo);
            var fotoPerfil = await _context.TBL_USUARIO_FOTO_PERFIL
                .FirstOrDefaultAsync(f => f.UsuarioId == usuarioId);

            if (fotoPerfil == null)
            {
                fotoPerfil = new UsuarioFotoPerfil
                {
                    UsuarioId = usuarioId,
                    MimeType = mimeType,
                    NomeArquivo = nomeArquivo,
                    Conteudo = conteudo,
                    DtCriacao = DateTimeOffset.UtcNow
                };

                await _context.TBL_USUARIO_FOTO_PERFIL.AddAsync(fotoPerfil);
            }
            else
            {
                fotoPerfil.MimeType = mimeType;
                fotoPerfil.NomeArquivo = nomeArquivo;
                fotoPerfil.Conteudo = conteudo;
                fotoPerfil.DtAtualizacao = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();
            return MapearFotoPerfil(fotoPerfil);
        }

        public async Task<bool> RemoverFotoPerfilAsync(int usuarioId)
        {
            var fotoPerfil = await _context.TBL_USUARIO_FOTO_PERFIL
                .FirstOrDefaultAsync(f => f.UsuarioId == usuarioId);

            if (fotoPerfil == null)
                return false;

            _context.TBL_USUARIO_FOTO_PERFIL.Remove(fotoPerfil);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Usuario?> AtualizarAsync(int usuarioId, UsuarioAtualizarDto dto)
        {
            var usuario = await _context.TBL_USUARIO.FindAsync(usuarioId);

            if (usuario == null)
                return null;

            var emailNormalizado = dto.Email.ToLower().Trim();

            if (usuario.Email != emailNormalizado)
            {
                var emailExiste = await _context.TBL_USUARIO
                    .AnyAsync(x => x.Email == emailNormalizado);

                if (emailExiste)
                    throw new InvalidOperationException("Email ja esta em uso.");
            }

            usuario.Nome = dto.Nome.Trim();
            usuario.Sobrenome = dto.Sobrenome.Trim();
            usuario.Email = emailNormalizado;

            if (!string.IsNullOrEmpty(dto.Password))
            {
                Criptografia.CriarPasswordHash(dto.Password, out byte[] hash, out byte[] salt);
                usuario.PasswordHash = hash;
                usuario.PasswordSalt = salt;
            }

            await _context.SaveChangesAsync();
            return usuario;
        }

        private static UsuarioFotoPerfilLeituraDto MapearFotoPerfil(UsuarioFotoPerfil fotoPerfil)
        {
            return new UsuarioFotoPerfilLeituraDto
            {
                AvatarUrl = MontarDataUrl(fotoPerfil),
                MimeType = fotoPerfil.MimeType,
                NomeArquivo = fotoPerfil.NomeArquivo,
                DtAtualizacao = fotoPerfil.DtAtualizacao ?? fotoPerfil.DtCriacao
            };
        }

        private static string MontarDataUrl(UsuarioFotoPerfil fotoPerfil)
        {
            return $"data:{fotoPerfil.MimeType};base64,{Convert.ToBase64String(fotoPerfil.Conteudo)}";
        }

        private static (string MimeType, byte[] Conteudo) ConverterDataUrl(string dataUrl)
        {
            if (string.IsNullOrWhiteSpace(dataUrl))
                throw new InvalidOperationException("Envie uma imagem valida para a foto de perfil.");

            var marker = ";base64,";
            var base64SeparatorIndex = dataUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (!dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                base64SeparatorIndex <= "data:".Length)
            {
                throw new InvalidOperationException("Formato de imagem invalido para a foto de perfil.");
            }

            var mimeType = dataUrl["data:".Length..base64SeparatorIndex].Trim();
            var base64 = dataUrl[(base64SeparatorIndex + marker.Length)..].Trim();

            try
            {
                return (mimeType, Convert.FromBase64String(base64));
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Formato de imagem invalido para a foto de perfil.");
            }
        }

        private static string SanitizarNomeArquivo(string? nomeArquivo)
        {
            var nomeFinal = Path.GetFileName(nomeArquivo?.Trim());
            return string.IsNullOrWhiteSpace(nomeFinal) ? "foto-perfil" : nomeFinal;
        }
    }
}
