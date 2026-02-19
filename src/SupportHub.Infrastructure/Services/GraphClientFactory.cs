namespace SupportHub.Infrastructure.Services;

using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using SupportHub.Application.Interfaces;

public class GraphClientFactory(IConfiguration _configuration) : IGraphClientFactory
{
    public GraphServiceClient CreateClient()
    {
        var tenantId = _configuration["AzureAd:TenantId"]!;
        var clientId = _configuration["AzureAd:ClientId"]!;
        var clientSecret = _configuration["AzureAd:ClientSecret"]!;

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        return new GraphServiceClient(credential);
    }
}
