using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Omnimarket.Api.Models.Configuracoes;
using Omnimarket.Api.Services.Interfaces;

namespace Omnimarket.Api.Services
{
    public class AzureBlobStorageService : IArquivoStorageService
    {
        private readonly AzureBlobStorageOptions _options;

        public AzureBlobStorageService(IOptions<AzureBlobStorageOptions> options)
        {
            _options = options.Value ?? new AzureBlobStorageOptions();
        }

        public async Task<string> SalvarAsync(
            string containerName,
            string diretorio,
            string nomeArquivo,
            string contentType,
            Stream conteudo,
            CancellationToken cancellationToken = default)
        {
            var containerClient = CriarContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobName = CriarBlobName(diretorio, nomeArquivo);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (conteudo.CanSeek)
                conteudo.Position = 0;

            await blobClient.UploadAsync(
                conteudo,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType
                    }
                },
                cancellationToken);

            return blobClient.Uri.ToString();
        }

        public async Task RemoverAsync(string? urlArquivo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(urlArquivo))
                return;

            if (!TryObterBlobInfo(urlArquivo, out var containerName, out var blobName))
                return;

            var containerClient = CriarContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
        }

        private BlobContainerClient CriarContainerClient(string containerName)
        {
            if (string.IsNullOrWhiteSpace(_options.ConnectionString) ||
                _options.ConnectionString.Contains("DEFINA_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configure AzureBlobStorage:ConnectionString em appsettings.Local.json, User Secrets ou variavel de ambiente.");
            }

            var nomeContainer = containerName?.Trim();
            if (string.IsNullOrWhiteSpace(nomeContainer))
            {
                throw new InvalidOperationException("Informe o container do Blob Storage que sera usado no upload.");
            }

            if (nomeContainer.Contains("SEU_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configure os nomes reais dos containers de imagens em AzureBlobStorage.");
            }

            return new BlobContainerClient(_options.ConnectionString, nomeContainer);
        }

        private static string CriarBlobName(string diretorio, string nomeArquivo)
        {
            var segmentos = diretorio
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizarSegmento)
                .Where(segmento => !string.IsNullOrWhiteSpace(segmento))
                .ToList();

            var nomeSeguro = Path.GetFileName(nomeArquivo?.Trim());
            if (string.IsNullOrWhiteSpace(nomeSeguro))
                nomeSeguro = "arquivo";

            segmentos.Add($"{Guid.NewGuid():N}-{nomeSeguro}");
            return string.Join("/", segmentos);
        }

        private bool TryObterBlobInfo(string urlArquivo, out string containerName, out string blobName)
        {
            containerName = string.Empty;
            blobName = string.Empty;

            if (!Uri.TryCreate(urlArquivo, UriKind.Absolute, out var blobUri))
                return false;

            var blobServiceClient = CriarBlobServiceClient();
            if (!Uri.TryCreate(blobServiceClient.Uri.ToString(), UriKind.Absolute, out var serviceUri))
                return false;

            if (!string.Equals(blobUri.Host, serviceUri.Host, StringComparison.OrdinalIgnoreCase))
                return false;

            var caminhoBlob = blobUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (caminhoBlob.Length <= 1)
                return false;

            containerName = caminhoBlob[0];
            if (!ContainerEhPermitido(containerName))
                return false;

            blobName = string.Join("/", caminhoBlob.Skip(1));
            return !string.IsNullOrWhiteSpace(blobName);
        }

        private bool ContainerEhPermitido(string containerName)
        {
            var containersPermitidos = new[]
            {
                _options.FotoPerfilContainerName,
                _options.FotoProdutoContainerName,
                _options.VideoProdutoContainerName
            };

            return containersPermitidos.Any(container =>
                !string.IsNullOrWhiteSpace(container) &&
                string.Equals(container.Trim(), containerName, StringComparison.OrdinalIgnoreCase));
        }

        private BlobServiceClient CriarBlobServiceClient()
        {
            if (string.IsNullOrWhiteSpace(_options.ConnectionString) ||
                _options.ConnectionString.Contains("DEFINA_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configure AzureBlobStorage:ConnectionString em appsettings.Local.json, User Secrets ou variavel de ambiente.");
            }

            return new BlobServiceClient(_options.ConnectionString);
        }

        private static string NormalizarSegmento(string valor)
        {
            return valor
                .Replace("\\", "/", StringComparison.Ordinal)
                .Trim('/')
                .Trim();
        }
    }
}
