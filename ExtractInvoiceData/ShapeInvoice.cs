using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using UiPath.CodedWorkflows;
using UiPath.IntelligentOCR.StudioWeb.Activities.DataExtraction;

namespace ExtractInvoiceData
{
    /// <summary>
    /// Reshapes an IXP extraction (project InvoicesIXP: Invoice Header, Vendor Information, Buyer Information, Line Items,
    /// Invoice Totals) into the exercise's invoice object — the same shape the generator writes, so the deterministic compare
    /// and the agent read one format. Extraction mistakes pass through untouched: that is the point of extracting at all.
    /// </summary>
    public class ShapeInvoice : CodedWorkflow
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true };

        [Workflow]
        public (string out_InvoiceJSON, string out_PONumber, string out_InvoiceNumber) Execute(IDocumentData<DictionaryData> in_Data)
        {
            var d = in_Data.Data;

            // IXP label groups arrive as fields whose values carry Components (the group's fields); a repeating group such
            // as Line Items arrives as one field with one value per row. Flatten both into a name → value map and a row list.
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lineRows = new List<Dictionary<string, string>>();
            var shapeLogged = false;
            try
            {
                var all = d.GetFields();
                if (all != null) foreach (var f in all)
                {
                    var isLines = Matches(f.FieldName, "Line Items") || Matches(f.FieldId, "Line Items");
                    var values = f.Values;
                    if (values == null || values.Length == 0) continue;
                    foreach (var v in values)
                    {
                        if (!shapeLogged) { shapeLogged = true; Log($"ResultsValue members: {string.Join(", ", v.GetType().GetProperties().Select(x => x.Name))}"); }
                        var comps = v.Components;
                        if (comps == null || comps.Length == 0)
                        {
                            foreach (var key in Keys(f.FieldName, f.FieldId)) if (!fields.ContainsKey(key)) fields[key] = (v.Value ?? "").Trim();
                            continue;
                        }
                        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var c in comps)
                        {
                            var cv = (c.Values != null && c.Values.Length > 0 ? c.Values[0]?.Value : null) ?? "";
                            foreach (var key in Keys(c.FieldName, c.FieldId))
                            {
                                if (!row.ContainsKey(key)) row[key] = cv.Trim();
                                if (!isLines && !fields.ContainsKey(key)) fields[key] = cv.Trim();
                            }
                        }
                        if (isLines) lineRows.Add(row);
                    }
                }
            }
            catch (Exception ex) { Log($"Reading the extraction failed: {ex.Message}", LogLevel.Warn); }
            Log($"IXP fields ({fields.Count}): {string.Join(" | ", fields.Where(k => !k.Key.Contains('.')).Select(k => k.Key + "=" + (k.Value.Length > 40 ? k.Value.Substring(0, 40) + "…" : k.Value)))}; line rows {lineRows.Count}");

            string Text(string field)
            {
                if (fields.TryGetValue(field, out var v)) return v;
                var squeezed = field.Replace(" ", "");
                foreach (var k in fields) if (k.Key.Replace(" ", "").EndsWith(squeezed, StringComparison.OrdinalIgnoreCase)) return k.Value;
                return "";
            }
            decimal? Num(string field) => ParseNumber(Text(field));
            string Cell(Dictionary<string, string> row, string name)
            {
                if (row.TryGetValue(name, out var v)) return v;
                var squeezed = name.Replace(" ", "");
                foreach (var k in row) if (k.Key.Replace(" ", "").EndsWith(squeezed, StringComparison.OrdinalIgnoreCase)) return k.Value;
                return "";
            }

            var lines = new List<object>();
            var n = 0;
            foreach (var row in lineRows)
            {
                var desc = Cell(row, "Description");
                if (desc.Length == 0 && Cell(row, "Line Total").Length == 0) continue;
                n++;
                lines.Add(new { n, description = desc, quantity = ParseNumber(Cell(row, "Quantity")) ?? 0m, unitPrice = ParseNumber(Cell(row, "Unit Price")) ?? 0m, lineTotal = ParseNumber(Cell(row, "Line Total")) ?? 0m });
            }

            var currency = Text("Currency");
            if (currency.Length == 0) currency = CurrencyOf(Text("Total Amount")) ?? CurrencyOf(Text("Subtotal")) ?? "";
            var taxRateText = Text("Tax Rate");
            var taxRate = ParseNumber(taxRateText) ?? 0m;

            var invoice = new
            {
                invoiceNumber = Text("Invoice Number"),
                poNumber = Text("Purchase Order Number"),
                invoiceDate = IsoDate(Text("Invoice Date")),
                dueDate = IsoDate(Text("Due Date")),
                currency,
                vendor = new { name = Text("Vendor Name"), address = Text("Vendor Address"), country = Text("Vendor Country"), taxId = Text("Vendor Tax ID") },
                buyer = new { name = Text("Buyer Name"), address = Text("Buyer Address"), country = Text("Buyer Country"), taxId = Text("Buyer Tax ID") },
                lines,
                subtotal = Num("Subtotal") ?? 0m,
                taxName = Text("Tax Name"),
                taxRate,
                taxTotal = Num("Tax Amount") ?? 0m,
                total = Num("Total Amount") ?? 0m
            };

            var json = JsonSerializer.Serialize(invoice, Json);
            Log($"Extracted {invoice.invoiceNumber} for {invoice.poNumber}: {lines.Count} line(s), total {invoice.total} {currency}");
            return (json, invoice.poNumber, invoice.invoiceNumber);
        }

        /// <summary>Every name a field can be found under: the display name, the id, and the id's last segment (after '.', '>' or '/').</summary>
        internal static IEnumerable<string> Keys(string name, string id)
        {
            if (!string.IsNullOrWhiteSpace(name)) { yield return name.Trim(); var i = name.LastIndexOfAny(new[] { '>', '.', '/' }); if (i >= 0) yield return name.Substring(i + 1).Trim(); }
            if (!string.IsNullOrWhiteSpace(id)) { yield return id.Trim(); var i = id.LastIndexOfAny(new[] { '>', '.', '/' }); if (i >= 0) yield return id.Substring(i + 1).Trim(); }
        }

        internal static bool Matches(string candidate, string wanted)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            var c = candidate.Replace(" ", "").Replace("-", "").Replace("_", ""); var w = wanted.Replace(" ", "");
            return c.Equals(w, StringComparison.OrdinalIgnoreCase) || c.EndsWith("." + w, StringComparison.OrdinalIgnoreCase) || c.EndsWith(">" + w, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"4,923.00 USD" → 4923.00; "19%" → 19; "1.234,50 EUR" → 1234.50. Null when nothing numeric is there.</summary>
        internal static decimal? ParseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var m = Regex.Match(text, @"-?\d[\d.,\s]*");
            if (!m.Success) return null;
            var s = m.Value.Replace(" ", "");
            // Decide the decimal separator by the last separator in the string.
            var lastComma = s.LastIndexOf(','); var lastDot = s.LastIndexOf('.');
            if (lastComma > lastDot) s = s.Replace(".", "").Replace(',', '.');
            else s = s.Replace(",", "");
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : (decimal?)null;
        }

        internal static string CurrencyOf(string text)
        {
            var m = Regex.Match(text ?? "", @"\b[A-Z]{3}\b");
            return m.Success ? m.Value : null;
        }

        /// <summary>IXP returns ISO timestamps ("2026-06-04T00:00:00Z") or printed dates; keep yyyy-MM-dd where it can be read.</summary>
        internal static string IsoDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)) return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return text.Trim();
        }
    }
}
