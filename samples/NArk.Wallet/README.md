# Arkade Wallet — Sample App

A production-quality neo-bank wallet built with the NNark dotnet SDK. Showcases all SDK features: wallets, VTXOs, spending, receiving, assets, and swaps.

## Architecture

```
┌─────────────────────────────────┐
│   Blazor WASM (PWA)             │  ← Browser
│   NArk.Wallet.Client            │
├─────────────────────────────────┤
│   REST API + SignalR             │
├─────────────────────────────────┤
│   ASP.NET Core Gateway           │  ← Server
│   NArk.Wallet.Gateway            │
│   ┌───────────┐ ┌─────────────┐ │
│   │ NArk SDK  │ │ EF Core     │ │
│   │ (Core +   │ │ SQLite      │ │
│   │  Swaps)   │ │ Storage     │ │
│   └─────┬─────┘ └─────────────┘ │
│         │ gRPC                   │
└─────────┼───────────────────────┘
          ▼
    ┌──────────┐
    │  arkd    │
    └──────────┘
```

The NNark SDK cannot run in the browser (NBitcoin, gRPC, secp256k1 dependencies), so the gateway hosts the SDK and exposes REST + SignalR APIs to the Blazor WASM frontend.

## Prerequisites

- .NET 8 SDK
- An arkd server (defaults to Mutinynet at `https://mutinynet.arkade.sh`)

## Quick Start

```bash
cd samples/NArk.Wallet/NArk.Wallet.Gateway
dotnet run
```

Open `https://localhost:5001` in your browser.

## Features Demonstrated

| Feature | SDK Interface | REST Endpoint |
|---------|--------------|---------------|
| Create wallet | `WalletFactory`, `IWalletStorage` | `POST /api/wallets` |
| Get balance | `ISpendingService.GetAvailableCoins` | `GET /api/vtxos/{id}/balance` |
| List VTXOs | `IVtxoStorage.GetVtxos` | `GET /api/vtxos/{id}` |
| Send payment | `ISpendingService.Spend` | `POST /api/spend` |
| Receive addresses | `IArkadeAddressProvider.GetNextContract` | `GET /api/receive/{id}` |
| List swaps | `ISwapStorage.GetSwaps` | `GET /api/swaps/{id}` |
| Issue asset | `IAssetManager.IssueAsync` | `POST /api/assets/issue` |
| Burn asset | `IAssetManager.BurnAsync` | `POST /api/assets/burn` |
| Real-time events | `IVtxoStorage.VtxosChanged` | SignalR `/hubs/wallet` |

## Configuration

Edit `appsettings.json` to change the network:

```json
{
  "ConnectionStrings": {
    "Wallet": "Data Source=arkade-wallet.db"
  }
}
```

To switch networks, modify the `ArkNetworkConfig` in `Program.cs`:
- `ArkNetworkConfig.Mainnet` — Production
- `ArkNetworkConfig.Mutinynet` — Signet (default)
- `ArkNetworkConfig.Regtest` — Local development

## Project Structure

```
samples/NArk.Wallet/
├── NArk.Wallet.Shared/     # DTOs shared between gateway and client
├── NArk.Wallet.Gateway/    # ASP.NET Core server (SDK host)
│   ├── Data/                # EF Core DbContext
│   ├── Endpoints/           # REST API endpoints
│   ├── Hubs/                # SignalR hub
│   └── Services/            # Gateway services
└── NArk.Wallet.Client/     # Blazor WASM PWA
    ├── Pages/               # Route pages (Home, Send, Receive, Swap, Assets)
    ├── Layout/              # App shell with bottom navigation
    ├── Services/            # API client, state management
    └── wwwroot/             # Static assets, CSS, PWA manifest
```
