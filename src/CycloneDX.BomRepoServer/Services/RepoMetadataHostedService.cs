using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace CycloneDX.BomRepoServer.Services;

public class RepoMetadataHostedService : IHostedService
{
    private readonly IRepoService _repoService;

    public RepoMetadataHostedService(IRepoService repoService)
    {
        _repoService = repoService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _repoService.EnsureMetadataAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}