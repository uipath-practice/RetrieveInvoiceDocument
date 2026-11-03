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

            string Text(string field) { try { return (d.GetFieldValue(field)?.Value ?? "").Trim(); } catch (Exception) { return ""; } }
            decimal? Num(string field) => ParseNumber(Text(field));

            var lines = new List<object>();
            try
            {
                var table = d.GetTable("Line Items");
                var tv = table?.GetValue();
                if (tv != null)
                {
                    int Col(string name) => Array.FindIndex(tv.ColumnInfo, c => string.Equals(c.FieldName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(c.FieldId, name, StringComparison.OrdinalIgnoreCase));
                    int cDesc = Col("Description"), cQty = Col("Quantity"), cPrice = Col("Unit Price"), cTotal = Col("Line Total");
                    var n = 0;
                    foreach (var row in tv.GetRows())
                    {
                        var cells = row.ToArray();
                        if (cells.Length == 0 || cells[0].IsHeader) continue;
                        string Cell(int i) => i < 0 || i >= cells.Length ? "" : (cells[i].GetValue()?.Value ?? "").Trim();
                        var desc = Cell(cDesc);
                        if (desc.Length == 0 && Cell(cTotal).Length == 0) continue;
                        n++;
                        lines.Add(new
                        {
                            n,
                            description = desc,
                            quantity = ParseNumber(Cell(cQty)) ?? 0m,
                            unitPrice = ParseNumber(Cell(cPrice)) ?? 0m,
                            lineTotal = ParseNumber(Cell(cTotal)) ?? 0m
                        });
                    }
                }
            }
            catch (Exception ex) { Log($"Line Items table not read: {ex.Message}", LogLevel.Warn); }

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
