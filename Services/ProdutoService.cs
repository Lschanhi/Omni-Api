using Microsoft.EntityFrameworkCore;
using Omnimarket.Api.Data;
using Omnimarket.Api.Models.Dtos.Produtos;
using Omnimarket.Api.Models.Dtos.Produtos.Midias;
using Omnimarket.Api.Models.Entidades;
using Omnimarket.Api.Models.Enum;
using Omnimarket.Api.Services.Interfaces;
using Omnimarket.Api.Utils;

namespace Omnimarket.Api.Services
{
    public class ProdutoService : IProdutoService
    {
        private readonly DataContext _context;
        private const string TipoAlteracaoEdicaoDados = "EdicaoDados";
        private const string TipoAlteracaoEstoque = "AtualizacaoEstoque";
        private const string TipoAlteracaoDesativacao = "DesativacaoLogica";
        private const string MensagemMidiaInvalida = "Formato de midia invalido para o produto.";

        public ProdutoService(DataContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProdutoLeituraDto>> GetAllAsync()
        {
            var produtos = await BaseQuery()
                .Where(p =>
                    p.Loja.Ativa &&
                    p.StatusPublicacao == StatusProduto.Publicado &&
                    p.Estoque > 0)
                .OrderByDescending(p => p.DtCriacao)
                .ThenBy(p => p.Nome)
                .ToListAsync();

            return produtos.Select(MapToDto).ToList();
        }

        public async Task<ProdutoLeituraDto?> GetByIdAsync(int id)
        {
            var produto = await BaseQuery()
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.Loja.Ativa &&
                    p.StatusPublicacao == StatusProduto.Publicado);

            return produto == null ? null : MapToDto(produto);
        }

        public async Task<ProdutoLeituraDto> CreateAsync(ProdutoCriacaoDto dto, int usuarioId)
        {
            var loja = await _context.TBL_LOJA
                .FirstOrDefaultAsync(l => l.UsuarioId == usuarioId);

            if (loja == null)
                throw new Exception("Crie uma loja antes de cadastrar produtos.");

            if (!loja.Ativa)
                throw new Exception("Sua loja precisa estar ativa para cadastrar produtos.");

            var produto = new Produto
            {
                Nome = NormalizarTextoObrigatorio(dto.Nome, "Informe um nome valido para o produto."),
                Categoria = NormalizarTextoObrigatorio(dto.Categoria, "Informe uma categoria valida para o produto."),
                Preco = dto.Preco,
                Estoque = dto.Estoque,
                Descricao = LimparOpcional(dto.Descricao),
                StatusPublicacao = dto.StatusPublicacao,
                LojaId = loja.Id,
                Loja = loja,
                DtCriacao = DateTimeOffset.UtcNow
            };

            AdicionarImagens(produto, dto.Imagens);

            _context.TBL_PRODUTO.Add(produto);
            await _context.SaveChangesAsync();

            return MapToDto(produto);
        }

        public async Task<bool> UpdateAsync(int id, ProdutoAtualizarDto dto, int usuarioId)
        {
            var produto = await _context.TBL_PRODUTO
                .Include(p => p.Loja)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto == null)
                return false;

            if (produto.Loja.UsuarioId != usuarioId)
                throw new UnauthorizedAccessException("Voce nao pode editar este produto.");

            if (produto.StatusPublicacao == StatusProduto.Desativado)
                throw new Exception("Produto desativado nao pode ser alterado pelo editar comum.");

            var descricaoNormalizada = dto.Descricao != null
                ? LimparOpcional(dto.Descricao)
                : produto.Descricao;

            var alterouPreco = dto.Preco.HasValue && produto.Preco != dto.Preco.Value;
            var alterouDescricao = dto.Descricao != null && produto.Descricao != descricaoNormalizada;

            if (!alterouPreco && !alterouDescricao)
                return true;

            decimal? precoAnterior = alterouPreco ? produto.Preco : null;
            decimal? precoNovo = alterouPreco ? dto.Preco!.Value : null;
            var descricaoAnterior = alterouDescricao ? produto.Descricao : null;
            var descricaoNova = alterouDescricao ? descricaoNormalizada : null;

            if (alterouPreco)
                produto.Preco = dto.Preco!.Value;

            if (dto.Descricao != null)
                produto.Descricao = descricaoNormalizada;

            var dataAlteracao = DateTimeOffset.UtcNow;
            produto.DtAtualizacao = dataAlteracao;

            RegistrarHistoricoProduto(
                produto,
                usuarioId,
                TipoAlteracaoEdicaoDados,
                dataAlteracao,
                precoAnterior: precoAnterior,
                precoNovo: precoNovo,
                descricaoAnterior: descricaoAnterior,
                descricaoNova: descricaoNova);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> AtualizarEstoqueAsync(int id, ProdutoAtualizarEstoqueDto dto, int usuarioId)
        {
            var produto = await _context.TBL_PRODUTO
                .Include(p => p.Loja)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto == null)
                return false;

            if (produto.Loja.UsuarioId != usuarioId)
                throw new UnauthorizedAccessException("Voce nao pode alterar o estoque deste produto.");

            if (produto.StatusPublicacao == StatusProduto.Desativado)
                throw new Exception("Produto desativado nao pode ter estoque alterado.");

            if (produto.Estoque == dto.Estoque)
                return true;

            var estoqueAnterior = produto.Estoque;
            produto.Estoque = dto.Estoque;
            var dataAlteracao = DateTimeOffset.UtcNow;
            produto.DtAtualizacao = dataAlteracao;

            RegistrarHistoricoProduto(
                produto,
                usuarioId,
                TipoAlteracaoEstoque,
                dataAlteracao,
                estoqueAnterior: estoqueAnterior,
                estoqueNovo: dto.Estoque);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(int id, int usuarioId)
        {
            var produto = await _context.TBL_PRODUTO
                .Include(p => p.Loja)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto == null)
                return false;

            if (produto.Loja.UsuarioId != usuarioId)
                throw new UnauthorizedAccessException("Voce nao pode excluir este produto.");

            if (produto.StatusPublicacao == StatusProduto.Desativado)
                return true;

            var dataAlteracao = DateTimeOffset.UtcNow;
            produto.StatusPublicacao = StatusProduto.Desativado;
            produto.DtAtualizacao = dataAlteracao;

            RegistrarHistoricoProduto(
                produto,
                usuarioId,
                TipoAlteracaoDesativacao,
                dataAlteracao,
                precoAnterior: produto.Preco,
                precoNovo: produto.Preco,
                estoqueAnterior: produto.Estoque,
                estoqueNovo: produto.Estoque,
                descricaoAnterior: produto.Descricao,
                descricaoNova: produto.Descricao);

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<PageResult<ProdutoLeituraDto>> GetPagedAsync(ProdutoFiltroDto filtro)
        {
            var page = filtro.Page < 1 ? 1 : filtro.Page;
            var pageSize = filtro.PageSize < 1 ? 10 : filtro.PageSize;
            pageSize = pageSize > 50 ? 50 : pageSize;

            var query = BaseQuery()
                .Where(p => p.Loja.Ativa);

            if (filtro.StatusPublicacao.HasValue)
                query = query.Where(p => p.StatusPublicacao == filtro.StatusPublicacao.Value);
            else
                query = query.Where(p => p.StatusPublicacao == StatusProduto.Publicado);

            if (!string.IsNullOrWhiteSpace(filtro.Nome))
            {
                var like = $"%{filtro.Nome.Trim()}%";
                query = query.Where(p => EF.Functions.Like(p.Nome, like));
            }

            if (!string.IsNullOrWhiteSpace(filtro.Categoria))
            {
                var like = $"%{filtro.Categoria.Trim()}%";
                query = query.Where(p => EF.Functions.Like(p.Categoria, like));
            }

            if (filtro.LojaId.HasValue)
                query = query.Where(p => p.LojaId == filtro.LojaId.Value);

            if (filtro.PrecoMinimo.HasValue)
                query = query.Where(p => p.Preco >= filtro.PrecoMinimo.Value);

            if (filtro.PrecoMaximo.HasValue)
                query = query.Where(p => p.Preco <= filtro.PrecoMaximo.Value);

            if (filtro.Disponivel.HasValue)
            {
                if (filtro.Disponivel.Value)
                    query = query.Where(p => p.StatusPublicacao == StatusProduto.Publicado && p.Estoque > 0);
                else
                    query = query.Where(p => p.StatusPublicacao != StatusProduto.Publicado || p.Estoque <= 0);
            }

            var total = await query.CountAsync();
            var produtos = await query
                .OrderByDescending(p => p.DtCriacao)
                .ThenBy(p => p.Nome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PageResult<ProdutoLeituraDto>
            {
                Items = produtos.Select(MapToDto).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private IQueryable<Produto> BaseQuery()
        {
            return _context.TBL_PRODUTO
                .Include(p => p.Midias)
                .Include(p => p.Loja)
                .AsQueryable();
        }

        private void SincronizarImagens(Produto produto, IEnumerable<string> imagens)
        {
            var entradasMidia = NormalizarImagens(imagens).ToList();
            var fotosAtuais = produto.Midias
                .Where(m => m.Tipo == TipoMidiaProduto.Foto)
                .ToList();

            if (fotosAtuais.Count > 0)
            {
                _context.ProdutoMidia.RemoveRange(fotosAtuais);
                foreach (var foto in fotosAtuais)
                    produto.Midias.Remove(foto);
            }

            var midiasNaoFoto = produto.Midias
                .Where(m => m.Tipo != TipoMidiaProduto.Foto)
                .OrderBy(m => m.Ordem)
                .ToList();

            for (var index = 0; index < midiasNaoFoto.Count; index++)
                midiasNaoFoto[index].Ordem = entradasMidia.Count + index;

            AdicionarImagens(produto, entradasMidia);
        }

        private static void AdicionarImagens(Produto produto, IEnumerable<string>? imagens)
        {
            var entradasMidia = NormalizarImagens(imagens).ToList();
            if (entradasMidia.Count == 0)
                return;

            if (entradasMidia.Count > ProdutoMidiaHelper.QuantidadeMaximaMidiasPorOperacao)
            {
                throw new InvalidOperationException(
                    $"Envie no maximo {ProdutoMidiaHelper.QuantidadeMaximaMidiasPorOperacao} midias por operacao.");
            }

            var ordemInicial = produto.Midias.Any() ? produto.Midias.Max(m => m.Ordem) + 1 : 0;

            for (var index = 0; index < entradasMidia.Count; index++)
            {
                produto.Midias.Add(CriarMidiaProduto(entradasMidia[index], ordemInicial + index));
            }
        }

        private static IEnumerable<string> NormalizarImagens(IEnumerable<string>? imagens)
        {
            if (imagens == null)
                return Array.Empty<string>();

            return imagens
                .Select(url => url?.Trim())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .ToList();
        }

        private static ProdutoMidia CriarMidiaProduto(string entradaMidia, int ordem)
        {
            if (ProdutoMidiaHelper.EhDataUrl(entradaMidia))
            {
                var (mimeType, conteudo) = ProdutoMidiaHelper.ConverterDataUrl(entradaMidia, MensagemMidiaInvalida);

                if (conteudo.Length > ProdutoMidiaHelper.TamanhoMaximoMidiaEmBytes)
                    throw new InvalidOperationException("Cada midia do produto deve ter no maximo 15 MB.");

                var tipo = ProdutoMidiaHelper.DeterminarTipoMidia(mimeType, null, MensagemMidiaInvalida);

                return new ProdutoMidia
                {
                    Tipo = tipo,
                    Url = string.Empty,
                    ContentType = mimeType,
                    NomeArquivo = ProdutoMidiaHelper.SanitizarNomeArquivo(null, tipo),
                    Conteudo = conteudo,
                    Ordem = ordem
                };
            }

            var tipoLegado = ProdutoMidiaHelper.DeterminarTipoMidia(null, entradaMidia, MensagemMidiaInvalida);

            return new ProdutoMidia
            {
                Tipo = tipoLegado,
                Url = entradaMidia,
                ContentType = null,
                NomeArquivo = ProdutoMidiaHelper.SanitizarNomeArquivo(entradaMidia, tipoLegado),
                Conteudo = null,
                Ordem = ordem
            };
        }

        private static string NormalizarTextoObrigatorio(string? valor, string mensagemErro)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new Exception(mensagemErro);

            return valor.Trim();
        }

        private static string? LimparOpcional(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            return valor.Trim();
        }

        private void RegistrarHistoricoProduto(
            Produto produto,
            int usuarioId,
            string tipoAlteracao,
            DateTimeOffset dataAlteracao,
            decimal? precoAnterior = null,
            decimal? precoNovo = null,
            int? estoqueAnterior = null,
            int? estoqueNovo = null,
            string? descricaoAnterior = null,
            string? descricaoNova = null)
        {
            _context.TBL_HISTORICO_PRODUTO.Add(new HistoricoProduto
            {
                ProdutoId = produto.Id,
                LojaId = produto.LojaId,
                UsuarioResponsavelId = usuarioId,
                TipoAlteracao = tipoAlteracao,
                NomeProdutoSnapshot = produto.Nome,
                CategoriaProdutoSnapshot = produto.Categoria,
                PrecoAnterior = precoAnterior,
                PrecoNovo = precoNovo,
                EstoqueAnterior = estoqueAnterior,
                EstoqueNovo = estoqueNovo,
                DescricaoAnterior = descricaoAnterior,
                DescricaoNova = descricaoNova,
                DataAlteracao = dataAlteracao
            });
        }

        private static ProdutoLeituraDto MapToDto(Produto produto)
        {
            var midiasOrdenadas = produto.Midias
                .OrderBy(m => m.Ordem)
                .ToList();

            return new ProdutoLeituraDto
            {
                Id = produto.Id,
                Nome = produto.Nome,
                Categoria = produto.Categoria,
                Preco = produto.Preco,
                Estoque = produto.Estoque,
                Disponivel = produto.Disponivel,
                StatusPublicacao = produto.StatusPublicacao,
                Descricao = produto.Descricao,
                MediaAvaliacao = produto.MediaAvaliacao,
                TotalAvaliacoes = produto.TotalAvaliacoes,
                DtCriacao = produto.DtCriacao,
                DtAtualizacao = produto.DtAtualizacao,
                LojaId = produto.LojaId,
                NomeLoja = produto.Loja != null ? produto.Loja.NomeFantasia : string.Empty,
                Imagens = midiasOrdenadas
                    .Where(m => m.Tipo == TipoMidiaProduto.Foto)
                    .Select(ProdutoMidiaHelper.ObterUrlLeitura)
                    .ToList(),
                Midias = midiasOrdenadas
                    .Select(ProdutoMidiaService.MapearMidia)
                    .ToList()
            };
        }
    }
}
