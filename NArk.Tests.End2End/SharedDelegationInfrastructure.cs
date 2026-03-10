using NArk.Tests.End2End.Core;

namespace NArk.Tests.End2End.Delegation;

[SetUpFixture]
public class SharedDelegationInfrastructure
{
    /// <summary>REST endpoint for wallet operations (port 7001 internal → 7011 external).</summary>
    public static readonly Uri DelegatorWalletEndpoint = new("http://localhost:7011");

    /// <summary>gRPC + delegator REST endpoint (port 7000 internal → 7010 external).
    /// Fulmine v0.3.15 runs the delegator HTTP interface on the same port as gRPC.</summary>
    public static readonly Uri DelegatorEndpoint = new("http://localhost:7010");

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        ThreadPool.SetMinThreads(50, 50);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Verify arkd and delegator are running
        foreach (var (name, url) in new[]
                 {
                     ("arkd", $"{SharedArkInfrastructure.ArkdEndpoint}/v1/info"),
                     ("delegator wallet", $"{DelegatorWalletEndpoint}/api/v1/wallet/status")
                 })
        {
            try
            {
                await http.GetAsync(url);
            }
            catch (Exception ex)
            {
                Assert.Fail(
                    $"{name} not running. Start infrastructure with:\n" +
                    "  cd NArk.Tests.End2End/Infrastructure && ./start-env.sh\n" +
                    "  (Windows: wsl bash ./start-env.sh)\n\n" +
                    $"Health check failed: {ex.Message}");
            }
        }
    }
}
