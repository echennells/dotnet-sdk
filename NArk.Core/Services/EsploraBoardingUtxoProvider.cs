using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NArk.Abstractions.Services;

namespace NArk.Core.Services;

/// <summary>
/// Queries boarding UTXOs from an Esplora-compatible API (e.g. mempool.space, Chopsticks).
/// </summary>
public class EsploraBoardingUtxoProvider : IBoardingUtxoProvider
{
    private readonly HttpClient _httpClient;

    public EsploraBoardingUtxoProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public EsploraBoardingUtxoProvider(Uri esploraBaseUri)
        : this(new HttpClient { BaseAddress = esploraBaseUri })
    {
    }

    public async Task<IReadOnlyList<BoardingUtxo>> GetUtxosAsync(string address, CancellationToken cancellationToken = default)
    {
        var utxos = await _httpClient.GetFromJsonAsync<EsploraUtxo[]>(
            $"address/{address}/utxo", cancellationToken);

        if (utxos is null)
            return [];

        return utxos.Select(u => new BoardingUtxo(
            Txid: u.Txid,
            Vout: (uint)u.Vout,
            Amount: (ulong)u.Value,
            Confirmed: u.Status?.Confirmed ?? false,
            BlockHeight: u.Status?.BlockHeight ?? 0,
            BlockTime: u.Status?.BlockTime ?? 0
        )).ToArray();
    }

    internal class EsploraUtxo
    {
        [JsonPropertyName("txid")]
        public string Txid { get; set; } = string.Empty;

        [JsonPropertyName("vout")]
        public int Vout { get; set; }

        [JsonPropertyName("value")]
        public long Value { get; set; }

        [JsonPropertyName("status")]
        public EsploraUtxoStatus? Status { get; set; }
    }

    internal class EsploraUtxoStatus
    {
        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }

        [JsonPropertyName("block_height")]
        public long BlockHeight { get; set; }

        [JsonPropertyName("block_time")]
        public long BlockTime { get; set; }
    }
}
