# Invoice Validation — the coded action app

The Action Center form of the Invoice Matching exercise (`courses/coding-agents`, lesson `invoice-matching`). A participant's process (Maestro BPMN or Maestro Flow) creates a task with this app whenever the agent returns yellow or red; the reviewer sees the invoice and its purchase order side by side with every finding highlighted, edits the e-mail the agent drafted for the supplier, and approves the payment or rejects the invoice. Provided in folder `Tools` of the course tenant, built once by the WTFactory Master — participants wire it in, they never build it.

| Path | Holds |
|---|---|
| `action-schema.json` | the task contract: inputs `InvoiceFileName`, `InvoiceJSON`, `POID`, `MatchResultJSON`; inOut `RejectionEmailHtml`; outputs `ReviewerComment`, `ReviewerEmail`; outcomes `Approve`, `Reject`. The descriptions are what a participant's coding agent reads from the deployed app, so the `MatchResultJSON` description carries the whole `matchResult` shape |
| `uipath.json` | the runtime OAuth client (External Application `Invoice Validation External App`, non-confidential) and the scopes the app uses |
| `src/components/Form.tsx` | the screen: header with the status, three tabs (**Compare**, **Invoice PDF**, **Purchase order page**), the decision footer, the e-mail drawer |
| `src/components/InvoicePane.tsx` · `PurchaseOrderPane.tsx` | the two documents of the Compare tab — the invoice as IXP extracted it, the purchase order as the generator's SAP ME23N screen — with the findings highlighted in their severity colour |
| `src/components/FindingsPanel.tsx` | the findings bar: one chip per finding, the selected one shows both values and the note and outlines its cells on both documents |
| `src/components/EmailEditor.tsx` | preview / edit of `RejectionEmailHtml`, prefilled from `matchResult.rejectionEmailHtml` when the process passes nothing |
| `src/highlight.ts` | maps a finding (`area`, `field`, `invoiceLine`, `poLine`) to the cells it marks; tolerant of the agent's spelling (`tax rate`, `unit price`, …) |
| `src/platform.ts` | the platform calls: the `PurchaseOrders` row by `POID` (Data Fabric, `PODATA` json + `POHTML` file), bucket `InvoicesStorage` by name in the app's own folder, the task's assignee as `ReviewerEmail`; and the local sample mode |
| `samples/` | a real generator run (INV-88757045 / PO-98476545, DESCRIPTION_SYNONYM) and a hand-written match result — what `npm run dev` shows without Action Center |
| `deploy.sh <version>` | stages `dist/` under the course's builds dir (the tenant login lives there) and runs pack → publish `-t Action` → deploy into `Tools` |

## Working on it

```bash
npm install
npm run dev            # http://localhost:5173 — local sample mode (no Action Center, no platform calls; the PDF and the original page stay empty)
npm run build          # tsc + vite; dist/ is what ships
./deploy.sh 1.0.2      # from anywhere; bumps nothing itself — pick the next version, Orchestrator refuses a re-publish of the same one
```

The app runs only inside Action Center: `CodedActionAppService.getTask()` needs the host, and the SDK (`new UiPath()`, never `initialize()`) reads the session Action Center injects. Local sample mode (`MOCK` in `src/platform.ts`: dev server and no `basedomain` query parameter) fakes the task from `samples/` and logs `setTaskData` / `completeTask` to the console.

## What the process must pass

| Field | Value |
|---|---|
| `InvoiceFileName` | the generator's `out_InvoiceFileName`, e.g. `INV-22150825.pdf` (bucket `InvoicesStorage`, folder `Tools`) |
| `InvoiceJSON` | `Extract Invoice Data (IXP)` → `out_InvoiceJSON`, verbatim |
| `POID` | the purchase order number; the app loads the `PurchaseOrders` row itself |
| `MatchResultJSON` | the agent's `matchResult` as JSON text (`status`, `findings[]`, `summary`, `rejectionEmailHtml`) |
| `RejectionEmailHtml` (inOut) | optional; empty lets the app prefill it from the match result. Comes back edited |

On completion the process reads the outcome (`Approve` / `Reject`), `RejectionEmailHtml` (send it on Reject), `ReviewerComment` and `ReviewerEmail` (→ `PaymentsQueue.ApprovedBy`).

## Platform access

| Call | Scope | Note |
|---|---|---|
| `PurchaseOrders` query by `POID`, `POHTML` download | `DataFabric.Data.Read` (+ `DataFabric.Schema.Read`) | entities are tenant-level; Data Fabric returns field names re-cased (`Poid`, `Podata`), read case-insensitively |
| bucket `InvoicesStorage` by name, PDF read URI (the PDF viewer widget) | `OR.Buckets.Read` | resolved in the app's **own** folder (`<meta name="uipath:folder-key">`, injected at deploy = `Tools`), not the task's — a participant's task sits in their personal workspace |
| task assignee → `ReviewerEmail` | `OR.Tasks.Read` | `Tasks.getById(taskId, {}, task.folderId)` |

The External Application is org-level, admin-only (Sergey creates it; the WTFactory Master may set its scopes and redirect): redirect URI `https://cloud.uipath.com/<orgId>/<tenantId>/actions_`, user scopes as in `uipath.json` (comma-separated on the CLI). A new tenant means a new redirect URI on the same app.
