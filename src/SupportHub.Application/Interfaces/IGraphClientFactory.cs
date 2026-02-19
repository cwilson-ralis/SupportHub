namespace SupportHub.Application.Interfaces;

using Microsoft.Graph;

public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}
