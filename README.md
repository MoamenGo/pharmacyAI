# PharmaAI

نظام إدارة صيدليات للسوق المصري، يعمل على الكمبيوتر بدون إنترنت.
Offline-first pharmacy management desktop app for Egypt.

## Layout

| Path | What it is |
| --- | --- |
| `backend/` | .NET 8 Minimal API, Clean Architecture (Domain, Application, Infrastructure, Api) with MediatR CQRS and FluentValidation |
| `backend/tests/` | xUnit tests: Application (validators, handlers) and Api (end-to-end over a real SQLite file) |
| `frontend/` | Next.js 14 (App Router, static export), TypeScript, Tailwind, Zustand, Dexie. Arabic RTL, dark mode |
| `frontend/src-tauri/` | Tauri v2 shell. Starts the API as a sidecar and stores the database in the app data folder |

## How the pieces talk

The Tauri app launches the published API (`pharmaai-api`) on startup and passes it the database path.
The API listens on `http://127.0.0.1:5197` only, and the UI calls it over HTTP.
SQLite runs in WAL mode; `GET /api/health` reports the journal mode so the UI can show it.

## Prerequisites

- .NET 8 SDK
- Node.js 20+
- Rust (stable) and the [Tauri v2 system dependencies](https://v2.tauri.app/start/prerequisites/) for your OS

## Commands

```bash
# Backend
cd backend
dotnet build
dotnet test
dotnet run --project src/PharmaAI.Api          # API on http://127.0.0.1:5197

# Add a migration after changing entities
dotnet tool restore
dotnet ef migrations add <Name> -p src/PharmaAI.Infrastructure -s src/PharmaAI.Api -o Persistence/Migrations

# Frontend in a browser (start the API first)
cd frontend
npm install
npm run dev                                    # http://localhost:3000

# Desktop app (publishes the API sidecar first)
npm run tauri dev
npm run tauri build                            # installer in src-tauri/target/release/bundle
```

When running the API with `dotnet run`, the database defaults to
`%LOCALAPPDATA%\PharmaAI\pharmaai.db` (Windows) or `~/.local/share/PharmaAI/pharmaai.db` (Linux).
Override it with `--Database:Path=<file>`.

## Keyboard shortcuts

| Screen | Key | Action |
| --- | --- | --- |
| Home | F2 | Focus the pharmacy name field |
| Home | F5 | Re-check the connection to the local API |
| Inventory | F2 | Add a new drug |
| Inventory | F3 | Focus search (a scanned barcode + Enter opens the drug, or offers to add it) |
| Inventory | F4 | Receive stock for the selected drug |
| Sale | F2 | New sale (press twice to discard a sale in progress) |
| Sale | F3 | Focus search (scan a barcode, or type a name and pick with ↑/↓ + Enter) |
| Sale | F4 | Switch the selected line between packs and strips |
| Sale | F6 | Edit the selected line's quantity (Enter returns to search) |
| Sale | F8 | Remove the selected line |
| Sale | F9 | Enter the amount paid (empty means exact change) |
| Sale | F10 | Save and print the receipt; after a sale, reprint it |

## Inventory rules

- Stock is stored per drug batch as an integer count of the smallest unit (`QuantityInSubUnits`: strips, ampoules).
  A pack holds `SubUnitsPerPack` of them; receiving takes packs plus loose sub-units.
- Deductions are FEFO: earliest expiry first, then earliest received; expired batches are never sold.
  A batch is sellable through its expiry date, judged in Cairo local time.
- Money crosses the API as integer piasters (`...Piasters` fields); the domain uses `decimal` EGP.
- Each batch row has a version token, so two concurrent deductions can never spend the same stock.

## Sale rules

- A sale is cash only and is saved in one step: every line takes its stock FEFO, or nothing is saved.
  Each line records the batches it came from, for recalls, returns and tax reporting.
- Lines sell whole packs at the public price or loose strips at the strip price (pack price ÷ strips, rounded to the piaster).
- Receipt numbers are sequential. If two tills save at once, the second retries with the next number and fresh stock.
- The receipt is printed with the system print dialog on 80 mm thermal paper; the pharmacy name comes from the Home screen.
