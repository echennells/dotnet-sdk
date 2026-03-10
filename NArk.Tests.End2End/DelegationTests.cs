using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NArk.Abstractions.Extensions;
using NArk.Core.Contracts;
using NArk.Tests.End2End.Core;
using NArk.Tests.End2End.TestPersistance;
using NArk.Transport.GrpcClient;

namespace NArk.Tests.End2End.Delegation;

public class DelegationTests
{
    [Test]
    public async Task CanGetDelegatorInfoViaRest()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync(
            $"{SharedDelegationInfrastructure.DelegatorEndpoint}/v1/delegator/info");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Delegator info endpoint returned {response.StatusCode}");

        var json = await response.Content.ReadFromJsonAsync<DelegatorInfoResponse>(
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        Assert.That(json?.Pubkey, Is.Not.Null.And.Not.Empty,
            "Delegator should return a non-empty public key");

        TestContext.Progress.WriteLine($"Delegator pubkey: {json!.Pubkey}");
        TestContext.Progress.WriteLine($"Delegator fee: {json.Fee}");
        TestContext.Progress.WriteLine($"Delegator address: {json.DelegatorAddress}");
    }

    [Test]
    public async Task CanGetDelegatorInfoViaGrpc()
    {
        var delegatorProvider = new GrpcDelegatorProvider(
            SharedDelegationInfrastructure.DelegatorEndpoint.ToString());

        var info = await delegatorProvider.GetDelegatorInfoAsync();

        Assert.That(info.Pubkey, Is.Not.Null.And.Not.Empty,
            "Delegator should return a non-empty public key via gRPC");

        TestContext.Progress.WriteLine($"Delegator pubkey (gRPC): {info.Pubkey}");
        TestContext.Progress.WriteLine($"Delegator fee (gRPC): {info.Fee}");
    }

    [Test]
    public async Task CanCreateDelegateContractWithDelegatorPubkey()
    {
        var clientTransport = new GrpcClientTransport(SharedArkInfrastructure.ArkdEndpoint.ToString());
        var serverInfo = await clientTransport.GetServerInfoAsync();

        // 1. Get delegator pubkey
        var delegatorProvider = new GrpcDelegatorProvider(
            SharedDelegationInfrastructure.DelegatorEndpoint.ToString());
        var delegatorInfo = await delegatorProvider.GetDelegatorInfoAsync();

        TestContext.Progress.WriteLine($"Delegator pubkey: {delegatorInfo.Pubkey}");

        // 2. Create wallet and derive delegate contract
        var walletProvider = new InMemoryWalletProvider(clientTransport);
        var walletId = await walletProvider.CreateTestWallet();

        var signer = await (await walletProvider.GetAddressProviderAsync(walletId))!
            .GetNextSigningDescriptor();
        var delegateKey = KeyExtensions.ParseOutputDescriptor(delegatorInfo.Pubkey, serverInfo.Network);

        var delegateContract = new ArkDelegateContract(
            serverInfo.SignerKey,
            serverInfo.UnilateralExit,
            signer,
            delegateKey);

        var arkAddress = delegateContract.GetArkAddress().ToString(false);
        TestContext.Progress.WriteLine($"Delegate contract address: {arkAddress}");

        // 3. Verify the contract has the expected structure
        var tapLeaves = delegateContract.GetTapScriptList();
        Assert.That(tapLeaves.Length, Is.EqualTo(3),
            "Delegate contract should have 3 tap leaves (delegate, forfeit, exit)");

        // 4. Verify round-trip parse via entity serialization
        var entity = delegateContract.ToEntity("test-wallet");
        var parsed = ArkDelegateContract.Parse(entity.AdditionalData, serverInfo.Network);
        Assert.That(parsed.GetArkAddress().ToString(false), Is.EqualTo(arkAddress),
            "Parsed contract should produce the same address");

        TestContext.Progress.WriteLine("Delegate contract creation + parse round-trip verified");
    }

    private record DelegatorInfoResponse(
        [property: JsonPropertyName("pubkey")] string? Pubkey,
        [property: JsonPropertyName("fee")] string? Fee,
        [property: JsonPropertyName("delegatorAddress")] string? DelegatorAddress);
}
