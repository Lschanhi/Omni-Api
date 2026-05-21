using Omnimarket.Api.Models.Dtos.Produtos;
using Omnimarket.Api.Models.Dtos.Produtos.Lojas;
using Omnimarket.Api.Models.Entidades;

namespace Omnimarket.Api.Tests;

public class ContratoSemSkuSlugTests
{
    [Fact]
    public void ProdutoContratos_NaoDevemExporSku()
    {
        Assert.Null(typeof(Produto).GetProperty("Sku"));
        Assert.Null(typeof(ProdutoCriacaoDto).GetProperty("Sku"));
        Assert.Null(typeof(ProdutoAtualizarDto).GetProperty("Sku"));
        Assert.Null(typeof(ProdutoLeituraDto).GetProperty("Sku"));
        Assert.Null(typeof(HistoricoProduto).GetProperty("SkuProdutoSnapshot"));
    }

    [Fact]
    public void LojaContratos_NaoDevemExporSlug()
    {
        Assert.Null(typeof(Loja).GetProperty("Slug"));
        Assert.Null(typeof(LojaCriacaoDto).GetProperty("Slug"));
        Assert.Null(typeof(LojaAtualizacaoDto).GetProperty("Slug"));
        Assert.Null(typeof(LojaLeituraDto).GetProperty("Slug"));
        Assert.Null(typeof(LojaGestaoLeituraDto).GetProperty("Slug"));
    }
}
