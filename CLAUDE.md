# CLAUDE.md — RetrieveInvoiceDocument: the Invoice Matching back end

**Purpose.** The provided side of the Invoice Matching exercise: a UiPath coded RPA project that generates a supplier invoice and its purchase order, plants a controlled discrepancy, writes an answer key, and uploads everything where the Invoice Matching lesson reads it. Beside it, the IXP extraction process (`ExtractInvoiceData/`) and the reviewer's coded action app (`InvoiceValidationApp/`). Participants never see or touch any of it; the lesson wires them in as existing processes and an existing app.

**Read first.** `README.md` (layout, shipping), `scenarios.md` (the matrix — the contract this project implements). The design of the exercise is `courses/coding-agents/cookbook/environment/invoice-matching-design.md`.

## Rules

- **The purchase order is the truth.** An injector edits the invoice only, one change, one colour. A clean pair is the factory output with nothing done to it, field-identical on everything the deterministic compare reads; every wording difference is a planted discrepancy.
- **A new discrepancy is a row in `scenarios.md` first**, then a class in `Discrepancies/Injectors.cs` registered in `Catalogue.Default()`, then a smoke rule (`totalEqual` / `totalDiffers` in `smoke/Program.cs`). The exercise set (`InExerciseSet`) stays small — six — and changes only with the lesson.
- **The manifest is the test oracle.** `expected.status` and `expected.findings` are what a run must prove; no participant solution reads the answer key.
- **Run the smoke test after touching anything** under `Core/`, `Factory/`, `Discrepancies/`, `Render/` or `Templates/`. It renders HTML, needs no robot and no tenant, and diffs every injector against a clean run of the same seed.
- **C# 12 / .NET 8 syntax only** in `RetrieveInvoiceDocument/`: the robot compiles the coded workflow with .NET 8. The smoke project targets net8.0 for the same reason.
- **Keep `targetFramework: Portable`** and the `itext7` dependencies: the PDF is HTML converted by pdfHTML (`Core/PdfGenerator.cs`), which the XAML analyser cannot see — the "dependency not used" warning is a false positive.
- **Entity ids in `Main.xaml` are per tenant.** Never edit them by hand; `tenant/repin-entities.sh` installs the binding and patches them. Never commit a `.entities/*.zip`.
- **The header colour on the PDF is deliberate** (decided 2026-11-03): green, salmon, red by expected outcome, a hint for the human reviewer. The agent only sees JSON.
- **The action app runs only inside Action Center.** `npm run dev` shows the local sample mode (`samples/`); the real task, the PDF and the purchase order page need a task created by a process. `uip codedapp` commands run from the course's builds dir (`InvoiceValidationApp/deploy.sh`), never from this tree, because the tenant login lives there. Always publish with `-t Action`.
- **Dates follow today on purpose**; a seed reproduces companies, products, numbers and the discrepancy, not the calendar.

## Verifying

```bash
cd smoke && ~/.dotnet/dotnet build -c Release && ~/.dotnet/dotnet bin/Release/net8.0/smoke.dll ../RetrieveInvoiceDocument --pdf
```

Output lands under `$TMPDIR/invoice-po-smoke/` — open a `PO-*.html` to check the SAP look and a `pdf-*/INV-*.pdf` for the invoice.
