namespace NArk.Abstractions.Services;

/// <summary>
/// Provides on-chain UTXO data for boarding addresses.
/// Implementations may use Esplora, NBXplorer, or other blockchain indexers.
/// </summary>
public interface IBoardingUtxoProvider
{
    Task<IReadOnlyList<BoardingUtxo>> GetUtxosAsync(string address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an on-chain UTXO at a boarding address.
/// </summary>
public record BoardingUtxo(
    string Txid,
    uint Vout,
    ulong Amount,
    bool Confirmed,
    long BlockHeight,
    long BlockTime);
