using Omnimarket.Api.Models.Dtos.Pedidos.Carrinho;
using Omnimarket.Api.Models.Dtos.Pedidos.Checkout;
using Omnimarket.Api.Models.Dtos.Pedidos.ItemPedido;
using Omnimarket.Api.Tests.Support;

namespace Omnimarket.Api.Tests;

public class CheckoutServiceTests
{
    [Fact]
    public async Task PrepararCheckoutAsync_DeveAgruparCarrinhoPorLojaERetornarOpcoesEntrega()
    {
        using var fixture = new ServiceTestFixture();
        var vendedorA = await fixture.CriarUsuarioAsync("seller-a");
        var vendedorB = await fixture.CriarUsuarioAsync("seller-b");
        var comprador = await fixture.CriarUsuarioAsync("buyer");
        var endereco = await fixture.CriarEnderecoAsync(comprador.Id);

        var lojaA = await fixture.CriarLojaAsync(vendedorA.Id, nomeFantasia: "Loja A", criarOpcaoRetiradaPadrao: false);
        var lojaB = await fixture.CriarLojaAsync(vendedorB.Id, nomeFantasia: "Loja B", criarOpcaoRetiradaPadrao: false);

        await fixture.CriarOpcaoEntregaLojaAsync(
            lojaA.Id,
            tipoEntregaId: (int)TipoEntrega.Correios,
            nome: "Correios Expresso A",
            valorFrete: 18m,
            prazoEntregaDias: 4);

        await fixture.CriarOpcaoEntregaLojaAsync(
            lojaB.Id,
            tipoEntregaId: (int)TipoEntrega.Retirada,
            nome: "Retirada Loja B",
            valorFrete: 0m,
            prazoEntregaDias: 0);

        var produtoA = await fixture.CriarProdutoAsync(vendedorA.Id, preco: 40m, estoque: 10);
        var produtoB = await fixture.CriarProdutoAsync(vendedorB.Id, preco: 25m, estoque: 10);

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoA.Id,
                Quantidade = 2
            });

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoB.Id,
                Quantidade = 1
            });

        var preparo = await fixture.CheckoutService.PrepararCheckoutAsync(comprador.Id, endereco.Id);

        Assert.Equal(endereco.Id, preparo.EnderecoId);
        Assert.Equal(3, preparo.TotalItens);
        Assert.Equal(105m, preparo.ValorTotalProdutos);
        Assert.Equal(2, preparo.Lojas.Count);

        var grupoLojaA = preparo.Lojas.Single(g => g.LojaId == lojaA.Id);
        Assert.Equal("Loja A", grupoLojaA.NomeLoja);
        Assert.Equal(2, grupoLojaA.TotalItens);
        Assert.Equal(80m, grupoLojaA.ValorTotalProdutos);
        Assert.Single(grupoLojaA.OpcoesEntrega);
        Assert.Equal("Correios Expresso A", grupoLojaA.OpcoesEntrega[0].Nome);

        var grupoLojaB = preparo.Lojas.Single(g => g.LojaId == lojaB.Id);
        Assert.Equal("Loja B", grupoLojaB.NomeLoja);
        Assert.Equal(1, grupoLojaB.TotalItens);
        Assert.Equal(25m, grupoLojaB.ValorTotalProdutos);
        Assert.Single(grupoLojaB.OpcoesEntrega);
        Assert.Equal("Retirada Loja B", grupoLojaB.OpcoesEntrega[0].Nome);
    }

    [Fact]
    public async Task FinalizarCheckoutAsync_DeveCriarUmPedidoPorLojaComFreteESnapshots()
    {
        using var fixture = new ServiceTestFixture();
        var vendedorA = await fixture.CriarUsuarioAsync("seller-checkout-a");
        var vendedorB = await fixture.CriarUsuarioAsync("seller-checkout-b");
        var comprador = await fixture.CriarUsuarioAsync("buyer-checkout");
        var endereco = await fixture.CriarEnderecoAsync(comprador.Id);

        var lojaA = await fixture.CriarLojaAsync(vendedorA.Id, nomeFantasia: "Loja Verde", criarOpcaoRetiradaPadrao: false);
        var lojaB = await fixture.CriarLojaAsync(vendedorB.Id, nomeFantasia: "Loja Azul", criarOpcaoRetiradaPadrao: false);

        var entregaA = await fixture.CriarOpcaoEntregaLojaAsync(
            lojaA.Id,
            tipoEntregaId: (int)TipoEntrega.EntregaLocal,
            nome: "Entrega Verde",
            valorFrete: 12m,
            prazoEntregaDias: 2);

        var entregaB = await fixture.CriarOpcaoEntregaLojaAsync(
            lojaB.Id,
            tipoEntregaId: (int)TipoEntrega.Correios,
            nome: "Correios Azul",
            valorFrete: 20m,
            prazoEntregaDias: 5);

        var produtoA = await fixture.CriarProdutoAsync(vendedorA.Id, preco: 50m, estoque: 10);
        var produtoB = await fixture.CriarProdutoAsync(vendedorB.Id, preco: 30m, estoque: 10);

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoA.Id,
                Quantidade = 2
            });

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoB.Id,
                Quantidade = 3
            });

        var resultado = await fixture.CheckoutService.FinalizarCheckoutAsync(
            comprador.Id,
            new CheckoutCriacaoDto
            {
                EnderecoId = endereco.Id,
                Observacao = "Checkout marketplace",
                Lojas =
                [
                    new CheckoutLojaCriacaoDto
                    {
                        LojaId = lojaA.Id,
                        LojaEntregaOpcaoId = entregaA.Id,
                        Observacao = "Entregar no periodo da tarde",
                        Itens =
                        [
                            new ItemPedidoDto
                            {
                                ProdutoId = produtoA.Id,
                                Quantidade = 2
                            }
                        ]
                    },
                    new CheckoutLojaCriacaoDto
                    {
                        LojaId = lojaB.Id,
                        LojaEntregaOpcaoId = entregaB.Id,
                        Itens =
                        [
                            new ItemPedidoDto
                            {
                                ProdutoId = produtoB.Id,
                                Quantidade = 3
                            }
                        ]
                    }
                ]
            });

        fixture.Context.ChangeTracker.Clear();

        var pedidos = await fixture.Context.TBL_PEDIDO
            .Include(p => p.Itens)
            .OrderBy(p => p.Id)
            .ToListAsync();

        Assert.True(resultado.SucessoTotal);
        Assert.False(resultado.SucessoParcial);
        Assert.Equal(2, resultado.QuantidadePedidosCriados);
        Assert.Equal(190m, resultado.ValorTotalProdutos);
        Assert.Equal(32m, resultado.ValorTotalFrete);
        Assert.Equal(222m, resultado.ValorTotalGeral);
        Assert.Empty(resultado.Falhas);
        Assert.Empty(resultado.CarrinhoAtualizado.Itens);

        Assert.Equal(2, pedidos.Count);

        var pedidoLojaA = pedidos.Single(p => p.LojaEntregaOpcaoId == entregaA.Id);
        Assert.Equal("Entrega Verde", pedidoLojaA.NomeEntregaSnapshot);
        Assert.Equal(2, pedidoLojaA.PrazoEntregaDias);
        Assert.Equal(12m, pedidoLojaA.ValorFrete);
        Assert.Equal(112m, pedidoLojaA.ValorTotalPedido);
        Assert.Single(pedidoLojaA.Itens);

        var pedidoLojaB = pedidos.Single(p => p.LojaEntregaOpcaoId == entregaB.Id);
        Assert.Equal("Correios Azul", pedidoLojaB.NomeEntregaSnapshot);
        Assert.Equal(5, pedidoLojaB.PrazoEntregaDias);
        Assert.Equal(20m, pedidoLojaB.ValorFrete);
        Assert.Equal(110m, pedidoLojaB.ValorTotalPedido);
        Assert.Single(pedidoLojaB.Itens);
    }

    [Fact]
    public async Task FinalizarCheckoutAsync_DeveRetornarFalhaParcialEManterItensNaoProcessadosNoCarrinho()
    {
        using var fixture = new ServiceTestFixture();
        var vendedorA = await fixture.CriarUsuarioAsync("seller-partial-a");
        var vendedorB = await fixture.CriarUsuarioAsync("seller-partial-b");
        var comprador = await fixture.CriarUsuarioAsync("buyer-partial");
        var endereco = await fixture.CriarEnderecoAsync(comprador.Id);

        var lojaA = await fixture.CriarLojaAsync(vendedorA.Id, nomeFantasia: "Loja Solar", criarOpcaoRetiradaPadrao: false);
        var lojaB = await fixture.CriarLojaAsync(vendedorB.Id, nomeFantasia: "Loja Lunar", criarOpcaoRetiradaPadrao: false);

        var entregaA = await fixture.CriarOpcaoEntregaLojaAsync(
            lojaA.Id,
            tipoEntregaId: (int)TipoEntrega.EntregaLocal,
            nome: "Entrega Solar",
            valorFrete: 10m,
            prazoEntregaDias: 1);

        var entregaB = await fixture.CriarOpcaoEntregaLojaAsync(
            lojaB.Id,
            tipoEntregaId: (int)TipoEntrega.Correios,
            nome: "Entrega Lunar",
            valorFrete: 14m,
            prazoEntregaDias: 3);

        var produtoA = await fixture.CriarProdutoAsync(vendedorA.Id, preco: 60m, estoque: 5);
        var produtoB = await fixture.CriarProdutoAsync(vendedorB.Id, preco: 35m, estoque: 5);

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoA.Id,
                Quantidade = 1
            });

        await fixture.CarrinhoService.AdicionarItemAsync(
            comprador.Id,
            new CarrinhoAdicionarDto
            {
                ProdutoId = produtoB.Id,
                Quantidade = 2
            });

        var resultado = await fixture.CheckoutService.FinalizarCheckoutAsync(
            comprador.Id,
            new CheckoutCriacaoDto
            {
                EnderecoId = endereco.Id,
                Lojas =
                [
                    new CheckoutLojaCriacaoDto
                    {
                        LojaId = lojaA.Id,
                        LojaEntregaOpcaoId = entregaA.Id,
                        Itens =
                        [
                            new ItemPedidoDto
                            {
                                ProdutoId = produtoA.Id,
                                Quantidade = 1
                            }
                        ]
                    },
                    new CheckoutLojaCriacaoDto
                    {
                        LojaId = lojaB.Id,
                        LojaEntregaOpcaoId = entregaA.Id,
                        Itens =
                        [
                            new ItemPedidoDto
                            {
                                ProdutoId = produtoB.Id,
                                Quantidade = 2
                            }
                        ]
                    }
                ]
            });

        fixture.Context.ChangeTracker.Clear();

        var pedidos = await fixture.Context.TBL_PEDIDO
            .Include(p => p.Itens)
            .ToListAsync();

        Assert.False(resultado.SucessoTotal);
        Assert.True(resultado.SucessoParcial);
        Assert.Single(resultado.Pedidos);
        Assert.Single(resultado.Falhas);
        Assert.Single(pedidos);

        var falha = resultado.Falhas.Single();
        Assert.Equal(lojaB.Id, falha.LojaId);
        Assert.Contains("opcao de entrega selecionada", falha.Mensagem, StringComparison.OrdinalIgnoreCase);

        Assert.Single(resultado.CarrinhoAtualizado.Itens);
        Assert.Equal(produtoB.Id, resultado.CarrinhoAtualizado.Itens[0].ProdutoId);
        Assert.Equal(2, resultado.CarrinhoAtualizado.Itens[0].Quantidade);
    }
}
