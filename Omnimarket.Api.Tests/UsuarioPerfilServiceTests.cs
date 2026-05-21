using Omnimarket.Api.Tests.Support;

namespace Omnimarket.Api.Tests;

public class UsuarioPerfilServiceTests
{
    [Fact]
    public async Task AtualizarAsync_DeveImpedirEmailDuplicado()
    {
        using var fixture = new ServiceTestFixture();
        var usuario1 = await fixture.CriarUsuarioAsync("usuario-perfil-1");
        var usuario2 = await fixture.CriarUsuarioAsync("usuario-perfil-2");

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.UsuarioPerfilService.AtualizarAsync(
            usuario1.Id,
            new UsuarioAtualizarDto
            {
                Nome = usuario1.Nome,
                Sobrenome = usuario1.Sobrenome,
                Email = usuario2.Email
            }));

        Assert.Equal("Email ja esta em uso.", excecao.Message);
    }

    [Fact]
    public async Task AtualizarFotoPerfilAsync_DevePersistirFotoEmTabelaDedicadaEExporNoPerfil()
    {
        using var fixture = new ServiceTestFixture();
        var usuario = await fixture.CriarUsuarioAsync("usuario-com-foto");
        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })}";

        var fotoPerfil = await fixture.UsuarioPerfilService.AtualizarFotoPerfilAsync(
            usuario.Id,
            new UsuarioFotoPerfilAtualizarDto
            {
                DataUrl = dataUrl,
                NomeArquivo = "avatar.png"
            });

        Assert.Equal(dataUrl, fotoPerfil.AvatarUrl);
        Assert.Equal("avatar.png", fotoPerfil.NomeArquivo);

        var perfil = await fixture.UsuarioPerfilService.ObterPerfilAsync(usuario.Id);

        Assert.NotNull(perfil);
        Assert.Equal(dataUrl, perfil!.AvatarUrl);
        Assert.Single(fixture.Context.TBL_USUARIO_FOTO_PERFIL);
    }

    [Fact]
    public async Task RemoverFotoPerfilAsync_DeveExcluirRegistroDaFoto()
    {
        using var fixture = new ServiceTestFixture();
        var usuario = await fixture.CriarUsuarioAsync("usuario-remove-foto");

        await fixture.UsuarioPerfilService.AtualizarFotoPerfilAsync(
            usuario.Id,
            new UsuarioFotoPerfilAtualizarDto
            {
                DataUrl = $"data:image/webp;base64,{Convert.ToBase64String(new byte[] { 9, 8, 7 })}",
                NomeArquivo = "avatar.webp"
            });

        var removida = await fixture.UsuarioPerfilService.RemoverFotoPerfilAsync(usuario.Id);

        Assert.True(removida);
        Assert.Empty(fixture.Context.TBL_USUARIO_FOTO_PERFIL);
    }
}
