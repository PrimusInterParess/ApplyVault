using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApplyVault.Api.IntegrationTests;

public sealed class ApplyVaultWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"applyvault-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("Testing:UseInMemoryDatabase", "true");
        builder.UseSetting("Testing:InMemoryDatabaseName", _databaseName);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:UseInMemoryDatabase"] = "true",
                ["Testing:InMemoryDatabaseName"] = _databaseName,
                ["ScrapeResultEnrichment:Enabled"] = "false",
                ["GoogleAi:Enabled"] = "false",
                ["MailIntegration:Enabled"] = "false",
                ["Supabase:Url"] = string.Empty
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where((descriptor) =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(GmailMailSyncBackgroundService))
                .ToList();

            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }

            var aiClientDescriptors = services
                .Where((descriptor) => descriptor.ServiceType == typeof(IScrapeResultAiClient))
                .ToList();

            foreach (var descriptor in aiClientDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IScrapeResultAiClient, NoOpScrapeResultAiClient>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthenticationOptions>((options) =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
