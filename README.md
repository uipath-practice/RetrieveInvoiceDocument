# RetrieveInvoiceDocument — the Invoice Matching back end

The three things participants' solutions **call but never build** in the Coding Agents course's Invoice Matching exercise (`courses/coding-agents`, lesson `invoice-matching`): one run produces a supplier invoice as a PDF in storage bucket `InvoicesStorage`, the purchase order it references as a row of Data Fabric entity `PurchaseOrders` (JSON plus an SAP ME23N-style HTML page), and an answer key beside the PDF. Beside it the IXP extraction process and the reviewer's action app. Version 2 (2026-11-03) replaced the 2025 generator of the Agentic Practice course: same company and product data, a new engine built like `generators/property-claims` — a clean pair, one planted discrepancy, a manifest, a smoke test.

| Path | Holds |
|---|---|
| `RetrieveInvoiceDocument/` | the RPA project (package id `RetrieveInvoiceDocument`, process `Retrieve Invoice Document`): `Main.xaml` wraps the coded workflow `GenerateInvoice.cs` and does the uploads; `Core/`, `Factory/`, `Discrepancies/`, `Render/`, `Models/` are the engine; `Templates/` the two HTML templates; `Data/` the CSVs |
| `ExtractInvoiceData/` | the second provided process (`Extract Invoice Data (IXP)`): downloads an invoice from `InvoicesStorage`, extracts it with the IXP project `InvoicesIXP` (Studio Web DU activity, `DictionaryData`, no typed bundle) and `ShapeInvoice.cs` returns it in the exercise's invoice JSON shape plus the purchase-order number |
| `InvoiceValidationApp/` | the coded action app `Invoice Validation` (React + `@uipath/coded-action-app` + the SDK): the reviewer's screen for yellow and red invoices — invoice and purchase order side by side with the findings highlighted, the supplier e-mail to edit, Approve / Reject. Its own `README.md` and `deploy.sh` |
| `tenant/ixp/` | the IXP project's exported taxonomy and the 15 generated invoices it was created from (`smoke --generate`); `tenant/repin-ixp.sh` pins the extraction project to the logged-in tenant's live version |
| `scenarios.md` | the matrix — fourteen injectors, their colours, the answer-key shape. The contract between generator, prompt and tests |
| `smoke/` | the only automated test: every injector against a clean pair of the same seed, outside UiPath |
| `tenant/create-entities.sh` · `tenant/entities/*.json` | the two Data Fabric entities (`PurchaseOrders`, `PaymentsQueue`); `tenant.py provision` runs the script (`data_fabric.entities[].managed_by`) |
| `tenant/repin-entities.sh` | pins `Main.xaml` to the logged-in tenant's `PurchaseOrders` entity (the Data Service activities carry entity and field ids as literals) |
| `pack.sh` | packs net8 with the pinned toolchain and uploads to the logged-in tenant's feed; copies the `.nupkg` to `dist/` (gitignored) |

## Shipping a change

```bash
cd smoke && ~/.dotnet/dotnet build -c Release && ~/.dotnet/dotnet bin/Release/net8.0/smoke.dll ../RetrieveInvoiceDocument   # must print OK
# bump projectVersion in RetrieveInvoiceDocument/project.json, then from the course's builds dir (the tenant login):
cd ~/Desktop/Builds/coding-agents
~/Projects/uipath-practice/generators/invoice-po/tenant/repin-entities.sh      # only when the tenant or its entity changed
~/Projects/uipath-practice/generators/invoice-po/pack.sh
# set feed.packages[RetrieveInvoiceDocument].version in courses/coding-agents/cookbook/environment/tenant-passport.yaml, then tenant.py provision
```

Toolchain pins (`@uipath/rpa-tool` 1.198.4, .NET 8 SDK) are the ClaimCase ones: `../property-claims/TOOLCHAIN.md`. Run every `uip` command from under the course's builds dir so the folder-local login is the right tenant.

## Contracts nothing may break silently

- **Argument names and the answer-key shape** are published to the lesson through `scenarios.md` and the exercise contract (`courses/coding-agents/cookbook/environment/invoice-matching-contract.json`). A rename is a version bump here and an edit there in the same pass.
- **The invoice JSON, the purchase-order JSON and `matchResult.findings`** share field names with the validation app (`InvoiceValidationApp/src/types.ts`, `highlight.ts`) and the extraction process (`ExtractInvoiceData/ShapeInvoice.cs`); `Models/Documents.cs` and `Models/Manifest.cs` are the source of truth.
- **The action schema is participant-facing.** A coding agent reads `InvoiceValidationApp/action-schema.json` from the deployed app, so its field names and descriptions are part of the exercise contract; a change there is a lesson change.
- **The entity binding is per tenant.** `.entities/EntitiesStore.json` and the ids in `Main.xaml` belong to whichever tenant `repin-entities.sh` last ran against; the committed state is `CodingAgentsCourse`.
