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
| Sale | F5 | Switch the payment method: cash, card, credit |
| Sale | F6 | Edit the selected line's quantity (Enter returns to search) |
| Sale | F7 | Enter a discount on the whole receipt (pounds, or a percentage like 10%) |
| Sale | F8 | Remove the selected line |
| Sale | F9 | Enter the amount paid (cash: empty means exact change; credit: what is paid now) |
| Sale | F10 | Save and print the receipt; after a sale, reprint it |
| Sales | F3 | Open a receipt by number |
| Sales | F9 | Return the quantities entered on the open receipt |
| Customers | F2 | Add a customer |
| Customers | F3 | Focus search (name or phone) |
| Customers | F9 | Enter a payment for the selected customer |
| Sync | F5 | Send queued receipts to the tax authority now |

## Inventory rules

- Stock is stored per drug batch as an integer count of the smallest unit (`QuantityInSubUnits`: strips, ampoules).
  A pack holds `SubUnitsPerPack` of them; receiving takes packs plus loose sub-units.
- Deductions are FEFO: earliest expiry first, then earliest received; expired batches are never sold.
  A batch is sellable through its expiry date, judged in Cairo local time.
- Money crosses the API as integer piasters (`...Piasters` fields); the domain uses `decimal` EGP.
- Each batch row has a version token, so two concurrent deductions can never spend the same stock.
- The New drug dialog can look a product up in the bundled Egyptian drug list (about 25,000 medicines, Arabic and
  English trade names, active ingredient, manufacturer and EGP price; CC0, see
  `backend/src/PharmaAI.Infrastructure/Catalog/README.md`). Picking one fills the names, price and a guess at the
  strips per box, all still editable. The list has no barcodes, so the barcode is always scanned.

## Sale rules

- A sale is saved in one step: every line takes its stock FEFO, or nothing is saved.
  Each line records the batches it came from, for recalls, returns and tax reporting.
- Lines sell whole packs at the public price or loose strips at the strip price (pack price ÷ strips, rounded to the piaster).
- Receipt numbers are sequential. If two tills save at once, the second retries with the next number and fresh stock.
- A sale is paid in cash, by card, or on credit. A card sale is paid in full and can keep the terminal's reference.
  A credit sale needs a customer; what is not paid up front goes on the customer's balance, and the customer pays it
  off later from the Customers screen. Two tills changing the same balance at once never lose either change.
- A discount applies to the whole receipt. It is shared across the lines in proportion to their totals (leftover
  piasters go to the biggest line), and each line's share is sent to ETA as a commercial discount.
- The day's sales list splits takings into cash, card, credit and account payments.
- The receipt is printed with the system print dialog on 80 mm thermal paper; the pharmacy name comes from the Home screen.

## Returns

- The Sales screen lists a day's receipts with its totals; a receipt opens by clicking it or typing its number (F3).
- Any part of a receipt can come back, in the unit it was sold in, until everything on it has been returned (F9).
  Stock goes back into the batches it was sold from, latest expiry first, even if they have expired since.
- The refund is the returned share of the line's amount after discount; the last return of a line gets exactly what is
  left, so refunds never add up to more than was paid. A cash sale is refunded in cash and a card sale on the card.
  On a credit sale the refund first comes off the customer's balance and only the rest is paid back in cash.
- The day's totals show returns and cash refunds, and what should be in the drawer: cash sales and payments collected
  from customers, less cash refunds.
- Not yet: returns are not sent to ETA as return receipts.

## Sync and the ETA e-receipt

- The e-receipt is optional and off by default; it is turned on from the Settings screen, and the Sync screen only
  appears in the menu while it is on (or while old receipts still wait). While it is off, sales are not queued at all,
  so turning it on later starts with that moment's sales.
- While it is on, every sale writes an outbox row in the same transaction, so a saved sale is never lost from the tax
  queue, even when the machine is offline.
- A background worker wakes after each sale (and every 30 s) and sends due receipts to the Egyptian Tax Authority
  e-receipt API v1.2. Failures retry with backoff (30 s doubling, capped at 1 h); the Sync screen lists them.
- Receipts are built in sale-number order and chained with `previousUUID`. A built receipt is frozen, so a resend
  carries the same UUID. A receipt ETA rejects can be corrected and resent from the Sync screen; it is rebuilt with
  `referenceOldUUID` pointing at the rejected one.
- Medicines are sent VAT-exempt (`T1` / `V003`, rate 0), packs as `BOX` and strips as `EA`, item code = the GS1 barcode.
  Payment is sent as `C` (cash), `V` (card) or `O` (credit, since ETA has no code for selling on account).
- Settings live on the Settings screen. Sending waits until every field is filled.
  The client secret and preshared key are never returned by the API; leave them empty to keep the saved values.
- Set `Sync:Worker=false` to run the API without the background worker (tests do this).
- Not yet: polling ETA for each receipt's final Valid/Invalid state after it is accepted, and signatures.
  ETA rejects receipts issued more than 24 hours earlier, so keep the till online at least once a day.
