using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NArk.Core.Enums;
using NArk.Core.Models.Options;
using NArk.Core.Services;
using NArk.Core.Transport;

namespace NArk.Core.Events;

/// <summary>
/// Event handler that polls for VTXO updates after a successful spend transaction broadcast.
/// This ensures the local VTXO state reflects the new outputs from the transaction.
/// </summary>
public class PostSpendVtxoPollingHandler(
    VtxoSynchronizationService vtxoSyncService,
    IClientTransport transport,
    IOptions<VtxoPollingOptions> options,
    ILogger<PostSpendVtxoPollingHandler>? logger = null
) : IEventHandler<PostCoinsSpendActionEvent>
{
    public async Task HandleAsync(PostCoinsSpendActionEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.State != ActionState.Successful)
        {
            logger?.LogDebug("Skipping VTXO polling for spend action with state {State}", @event.State);
            return;
        }

        if (@event.Psbt is null)
        {
            return;
        }

        var delay = options.Value.TransactionBroadcastPollingDelay;

        // Wait for the configured delay to avoid race conditions with server persistence
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        try
        {
            var inputScripts = @event.ArkCoins.Select(c => c.ScriptPubKey.ToHex()).ToHashSet();
            var inputOutpoints = @event.ArkCoins.Select(c => c.Outpoint).ToList();
            // Only include P2TR scripts (0x5120 + 32-byte key = 34 bytes hex "5120...").
            // This filters out OP_RETURN outputs such as the asset packet and the
            // Ark anchor marker, which the arkd indexer rejects with "invalid script, must be P2TR".
            var outputScripts = @event.Psbt.Outputs
                .Select(o => o.ScriptPubKey.ToHex())
                .Where(s => s.StartsWith("5120") && s.Length == 68)
                .ToHashSet();

            var scripts = inputScripts.Union(outputScripts).ToHashSet();

            logger?.LogInformation(
                "PostSpendVtxoPolling: TxId={TxId}, delay={Delay}ms, inputScripts=[{InputScripts}], outputScripts=[{OutputScripts}]",
                @event.TransactionId, delay.TotalMilliseconds,
                string.Join(", ", inputScripts),
                string.Join(", ", outputScripts));

            // Retry with backoff — arkd's indexer may not have processed the VTXOs yet.
            // We poll all scripts to upsert both input (spent) and output (new) VTXOs,
            // then use the transport's spent_only filter to verify inputs are marked spent
            // by arkd before breaking — avoids relying on local storage which may lag.
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var found = await vtxoSyncService.PollScriptsForVtxos(scripts, cancellationToken);
                logger?.LogInformation(
                    "PostSpendVtxoPolling: attempt {Attempt}/{Max} for TxId={TxId}, {Found} VTXOs returned",
                    attempt, maxAttempts, @event.TransactionId, found);

                if (found > 0)
                {
                    // Ask arkd directly: are the input outpoints spent?
                    var spentCount = 0;
                    await foreach (var _ in transport.GetVtxosByOutpoints(inputOutpoints, spentOnly: true, cancellationToken))
                        spentCount++;

                    if (spentCount >= inputOutpoints.Count)
                        break;

                    logger?.LogInformation(
                        "PostSpendVtxoPolling: attempt {Attempt}/{Max} for TxId={TxId} — {Spent}/{Total} inputs spent on arkd",
                        attempt, maxAttempts, @event.TransactionId,
                        spentCount, inputOutpoints.Count);
                }

                if (attempt < maxAttempts)
                    await Task.Delay(delay, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(0, ex, "Failed to poll VTXOs after spend transaction {TxId}", @event.TransactionId);
            // Don't rethrow - event handlers shouldn't fail the main flow
        }
    }
}
