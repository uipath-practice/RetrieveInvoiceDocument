# Scenarios — what the generator plants and what the agent must say

Three readers share this table, which is why it lives beside the generator and nowhere else: the **generator** (which discrepancy to inject, `RetrieveInvoiceDocument/Discrepancies/Injectors.cs`), the **lesson prompt** (which colour each case earns), and the **tests** (what the answer key asserts). A discrepancy that is not a row here does not exist.

## How a run is aimed

`Retrieve Invoice Document` takes four arguments; all optional.

| Argument | Values | Default |
|---|---|---|
| `in_Discrepancy` | an id from the table, or `none` for a clean pair | empty → draw by probability |
| `in_DiscrepancyProbability` | 0–100: the chance an unpinned invoice carries one discrepancy **from the exercise set** (★) | 20 |
| `in_Seed` | non-zero integer — the same seed reproduces the same companies, products, numbers and discrepancy; dates follow today | 0 → fresh, recorded in the answer key |
| `in_LinesQuantity` | 2–8 lines on the purchase order | 4 |

**At most one discrepancy per invoice.** An unknown id fails the job rather than falling back to random, so a typo cannot quietly invalidate a test. The purchase order is never edited: every discrepancy is on the invoice, and the purchase order in Data Fabric is the truth.

## Outputs

| Output | Holds |
|---|---|
| `out_InvoiceFileName` | the PDF's name in storage bucket `InvoicesStorage`, `INV-<8 digits>.pdf` |
| `out_InvoiceNumber`, `out_PONumber` | the keys |
| bucket `InvoicesStorage/INV-<n>-answer.json` | the answer key: seed, discrepancy, expected status and findings (shape below) |
| Data Fabric `PurchaseOrders` row | `POID`, `PODATA` (the order as JSON, the contract shape), `POHTML` (the order as an SAP ME23N screen, for the validation app) |

## The matrix

`Expected` is the colour the agent must return. Every row fails the deterministic compare (field-identical or not); only a clean pair passes it. `Header` is the colour band the PDF carries as the reviewer's hint — the agent never sees the PDF, only JSON.

| Id | What the invoice shows versus the purchase order | Totals | Expected | Header |
|---|---|---|---|---|
| ★ `VENDOR_MISMATCH` | another company's name, address or tax id — one, two or all three (three variants, drawn by seed) | equal | red | dark red |
| ★ `TAX_RATE_MISMATCH` | a different tax name and rate (VAT 19 % vs GST 9 %); every line matches | differ | red | dark red |
| ★ `EXTRA_LINE_PACKAGING` | one PO line billed as the item at 80 % plus a "… packaging" line at 20 %, same quantity | equal | yellow | salmon |
| ★ `EXTRA_LINE_UNORDERED` | one extra line for a product not on the PO | differ | red | dark red |
| ★ `DESCRIPTION_SYNONYM` | one or two lines reworded — an adjective, the alternative name, a pack size ("Pen (1 box / 10 pcs)") | equal | yellow | salmon |
| ★ `DESCRIPTION_DIFFERENT_ITEM` | one line names another product at the PO's quantity and price | equal | red | dark red |
| `FORMATTING_ONLY` | the vendor's legal suffix spelled out ("Pte Ltd" → "Pte. Ltd.") and one description in capitals | equal | green | dark green |
| `SPLIT_LINES_VALID` | one PO line as two or three colour variants at the same unit price, quantities add up | equal | yellow | salmon |
| `SPLIT_LINES_INVALID` | the same split with one part over-counted | differ | red | dark red |
| `PRICE_WITHIN_TOLERANCE` | one unit price off by 0.5–2 % either way | differ | yellow | salmon |
| `QUANTITY_OVERBILLED` | one line with more units than ordered | differ | red | dark red |
| `PARTIAL_DELIVERY` | only some PO lines invoiced; the invoiced lines match | differ | yellow | salmon |
| `PO_NUMBER_UNKNOWN` | a purchase-order number that exists nowhere — the lookup finds nothing | equal | red | dark red |
| `CURRENCY_MISMATCH` | another currency, every number unchanged | equal | red | dark red |
| *(none)* | identical on every compared field | equal | green | dark green |

The six ★ rows are the exercise set a random draw picks from; the rest exist for pinned test cases and later variants. The clean invoice is **field-identical** to the purchase order on vendor, buyer, tax, every line (description, quantity, unit price, line total) and the totals, so the deterministic compare has something to pass; any wording difference is a planted discrepancy, never noise.

## The answer key

```json
{
  "invoiceNumber": "INV-88757045", "poNumber": "PO-98476545", "invoiceFileName": "INV-88757045.pdf",
  "seed": 4242, "linesQuantity": 4,
  "discrepancy": "VENDOR_MISMATCH",
  "discrepancyDescription": "The invoice names another vendor: …",
  "expected": {
    "status": "red", "deterministicMatch": false,
    "findings": [{ "area": "vendor", "field": "taxId", "severity": "red", "invoiceValue": "RO-55644633", "poValue": "RO-55664433", "note": "…" }]
  },
  "generatedAt": "2026-11-03 15:36:12"
}
```

`expected.findings` has the shape the agent returns in `matchResult.findings` (the exercise contract, `courses/coding-agents/cookbook/environment/invoice-matching-contract.json`): `area` is `vendor | buyer | tax | currency | line | total | po`, `invoiceLine` / `poLine` are 1-based line numbers where the finding is about a line. A test compares the agent's status with `expected.status`; the findings are what a reviewer should see highlighted.

## Verifying a change

```bash
cd smoke && ~/.dotnet/dotnet build -c Release && ~/.dotnet/dotnet bin/Release/net8.0/smoke.dll ../RetrieveInvoiceDocument --pdf
```

Every injector runs against a clean pair of the same seed at 2, 5 and 8 lines: the purchase order must be untouched, the invoice must differ and fail the deterministic compare, the manifest must name the injector and its colour, the `Totals` column above must hold, 2,000 draws at probability 20 must land near 20 % from the exercise set only. `--pdf` also converts three invoices.
