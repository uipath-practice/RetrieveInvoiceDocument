using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using InvoiceGenerator.Core;
using InvoiceGenerator.Discrepancies;
using InvoiceGenerator.Models;

// The generator's only automated test. Runs the data path outside UiPath with HTML output (iText never
// loads), so nothing needs a robot or a tenant. The load-bearing check is the diff: every injector runs
// against a clean pair of the same seed, the purchase order must be untouched, the invoice must differ,
// the manifest must name the injector and its colour, and the money rules of scenarios.md must hold.
//
//   cd smoke && dotnet build -c Release && dotnet bin/Release/net8.0/smoke.dll ../RetrieveInvoiceDocument
//   add --pdf to also convert one invoice and one of each colour to PDF (needs iText fonts, slower)
public static class Program
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static int Main(string[] argv)
    {
        var root = argv.FirstOrDefault(a => !a.StartsWith("--")) ?? Directory.GetCurrentDirectory();
        var pdf = argv.Contains("--pdf");
        root = Path.GetFullPath(root);

        // --generate <dir> <count>: write <count> invoice PDFs (clean and planted, every injector in turn) into <dir> — the
        // training set for an IXP project. Nothing else runs.
        var gi = Array.IndexOf(argv, "--generate");
        if (gi >= 0)
        {
            var dir = Path.GetFullPath(argv[gi + 1]); var count = int.Parse(argv[gi + 2]);
            var all = Catalogue.Default().Catalogue.Select(i => i.Id).Prepend("none").ToList();
            Directory.CreateDirectory(dir);
            for (var i = 0; i < count; i++)
            {
                var pin = all[i % all.Count];
                var r = new Pipeline().Run(new GenerationConfig { Discrepancy = pin, Seed = 9000 + i * 7, LinesQuantity = 2 + i % 6, OutputFolder = Path.Combine(dir, "_work"), OutputFormat = "pdf" }, root);
                File.Copy(r.InvoiceFile, Path.Combine(dir, Path.GetFileName(r.InvoiceFile)), true);
            }
            Directory.Delete(Path.Combine(dir, "_work"), true);
            Console.WriteLine($"{count} invoice PDFs in {dir}");
            return 0;
        }
        var outRoot = Path.Combine(Path.GetTempPath(), "invoice-po-smoke");
        if (Directory.Exists(outRoot)) Directory.Delete(outRoot, true);
        var failures = new List<string>();
        var engine = Catalogue.Default();

        Console.WriteLine($"Catalogue: {engine.Catalogue.Count} injectors, {engine.ExerciseSet.Count()} in the exercise set\n");

        // Rules: which injectors must keep the invoice total equal to the PO's, which must not.
        var totalEqual = new HashSet<string> { "VENDOR_MISMATCH", "EXTRA_LINE_PACKAGING", "DESCRIPTION_SYNONYM", "DESCRIPTION_DIFFERENT_ITEM", "FORMATTING_ONLY", "SPLIT_LINES_VALID", "PO_NUMBER_UNKNOWN", "CURRENCY_MISMATCH" };
        var totalDiffers = new HashSet<string> { "TAX_RATE_MISMATCH", "EXTRA_LINE_UNORDERED", "SPLIT_LINES_INVALID", "PRICE_WITHIN_TOLERANCE", "QUANTITY_OVERBILLED", "PARTIAL_DELIVERY" };

        foreach (var injector in engine.Catalogue)
        {
            for (var lines = 2; lines <= 8; lines += 3)   // 2, 5, 8 lines
            {
                var seed = 1000 + Math.Abs(StableHash(injector.Id) % 50000) + lines;
                var clean = Run(root, outRoot, "none", seed, lines, $"clean-{injector.Id}-{lines}");
                var dirty = Run(root, outRoot, injector.Id, seed, lines, $"dirty-{injector.Id}-{lines}");
                var tag = $"{injector.Id}/{lines}";

                if (dirty.Manifest.Discrepancy != injector.Id) failures.Add($"{tag}: manifest names {dirty.Manifest.Discrepancy}");
                if (dirty.Manifest.Expected.Status != injector.Expected) failures.Add($"{tag}: expected {injector.Expected}, manifest says {dirty.Manifest.Expected.Status}");
                if (dirty.Manifest.Expected.Findings.Count == 0) failures.Add($"{tag}: no findings recorded");
                if (dirty.Manifest.Expected.Findings.Any(f => f.Severity != injector.Expected)) failures.Add($"{tag}: a finding carries another severity than {injector.Expected}");
                if (clean.Manifest.Discrepancy != null || clean.Manifest.Expected.Status != Status.Green || !clean.Manifest.Expected.DeterministicMatch) failures.Add($"{tag}: the clean run is not clean");
                if (clean.Manifest.PoNumber != dirty.Manifest.PoNumber || clean.Manifest.InvoiceNumber != dirty.Manifest.InvoiceNumber) failures.Add($"{tag}: the seed is not deterministic (different numbers)");

                var cleanPo = JsonSerializer.Serialize(clean.PurchaseOrder, Json); var dirtyPo = JsonSerializer.Serialize(dirty.PurchaseOrder, Json);
                if (cleanPo != dirtyPo) failures.Add($"{tag}: the injector edited the purchase order");
                var cleanInv = JsonSerializer.Serialize(clean.Invoice, Json); var dirtyInv = JsonSerializer.Serialize(dirty.Invoice, Json);
                if (cleanInv == dirtyInv) failures.Add($"{tag}: the injector changed nothing on the invoice");

                // A clean invoice is field-identical to its PO on everything the deterministic compare reads.
                if (!FieldIdentical(clean.Invoice, clean.PurchaseOrder, out var why)) failures.Add($"{tag}: clean invoice differs from its PO: {why}");
                if (FieldIdentical(dirty.Invoice, dirty.PurchaseOrder, out _)) failures.Add($"{tag}: the dirty invoice still passes the deterministic compare");

                if (totalEqual.Contains(injector.Id) && dirty.Invoice.Total != dirty.PurchaseOrder.Total) failures.Add($"{tag}: total should equal the PO ({dirty.Invoice.Total} vs {dirty.PurchaseOrder.Total})");
                if (totalDiffers.Contains(injector.Id) && dirty.Invoice.Total == dirty.PurchaseOrder.Total) failures.Add($"{tag}: total should differ from the PO");
                if (dirty.Invoice.Subtotal != Money.Round(dirty.Invoice.Lines.Sum(l => l.LineTotal))) failures.Add($"{tag}: subtotal does not sum the lines");
                if (dirty.Invoice.Lines.Any(l => l.LineTotal != Money.Round(l.Quantity * l.UnitPrice))) failures.Add($"{tag}: a line total is not quantity × price");
                if (dirty.Invoice.Lines.Select((l, i) => l.N == i + 1).Any(ok => !ok)) failures.Add($"{tag}: line numbers are not 1..n");
                if (dirty.Manifest.Expected.Findings.Any(f => f.InvoiceLine is int n && (n < 1 || n > dirty.Invoice.Lines.Count))) failures.Add($"{tag}: a finding points at an invoice line that does not exist");
                if (dirty.Manifest.Expected.Findings.Any(f => f.PoLine is int n && (n < 1 || n > dirty.PurchaseOrder.Lines.Count))) failures.Add($"{tag}: a finding points at a PO line that does not exist");

                foreach (var f in new[] { dirty.InvoiceFile, dirty.PurchaseOrderHtmlFile })
                    if (File.ReadAllText(f).Contains("{{")) failures.Add($"{tag}: unfilled token in {Path.GetFileName(f)}");

                Console.WriteLine($"  {tag,-32} {dirty.Manifest.Expected.Status,-6} {dirty.Manifest.Expected.Findings.Count} finding(s)  total {dirty.Invoice.Total} vs {dirty.PurchaseOrder.Total}");
            }
        }

        // Draws: probability 0 never plants; 20 lands near 20 % and only from the exercise set; 100 always plants.
        Console.WriteLine("\nDraws");
        var draws = new Dictionary<string, int>(); var planted = 0; const int N = 2000;
        for (var i = 0; i < N; i++)
        {
            var id = new Identity(new InvoiceGenerator.Data.Catalog(Path.Combine(root, "Data")), 7 + i * 13, 4);
            var pick = engine.Choose("", 20, id.Stream("discrepancy"));
            if (pick == null) continue;
            planted++; draws[pick.Id] = draws.TryGetValue(pick.Id, out var c) ? c + 1 : 1;
            if (!pick.InExerciseSet) failures.Add($"draw picked {pick.Id}, which is not in the exercise set");
        }
        var rate = 100.0 * planted / N;
        Console.WriteLine($"  probability 20 → {rate:0.0} % planted over {N} seeds; {string.Join(", ", draws.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))}");
        if (rate < 16 || rate > 24) failures.Add($"draw rate {rate:0.0} % is not near 20 %");
        if (draws.Count != engine.ExerciseSet.Count()) failures.Add("not every exercise injector was drawn");
        if (engine.Choose("", 0, new Random(1)) != null) failures.Add("probability 0 planted something");
        if (Enumerable.Range(0, 50).Any(i => engine.Choose("", 100, new Random(i)) == null)) failures.Add("probability 100 left a pair clean");
        try { engine.Choose("NOT_A_THING", 20, new Random(1)); failures.Add("an unknown pin did not throw"); } catch (ArgumentException) { }

        if (pdf)
        {
            Console.WriteLine("\nPDF");
            foreach (var pin in new[] { "none", "DESCRIPTION_SYNONYM", "VENDOR_MISMATCH" })
            {
                var r = new Pipeline().Run(new GenerationConfig { Discrepancy = pin, Seed = 4242, OutputFolder = Path.Combine(outRoot, "pdf-" + pin), OutputFormat = "pdf" }, root);
                var size = new FileInfo(r.InvoiceFile).Length;
                Console.WriteLine($"  {pin,-24} {Path.GetFileName(r.InvoiceFile)} {size / 1024} KB");
                if (size < 2000) failures.Add($"pdf for {pin} is suspiciously small ({size} bytes)");
            }
        }

        Console.WriteLine();
        if (failures.Count == 0) { Console.WriteLine($"OK — output under {outRoot}"); return 0; }
        foreach (var f in failures) Console.WriteLine("FAIL " + f);
        Console.WriteLine($"{failures.Count} failure(s) — output under {outRoot}");
        return 1;
    }

    private static GenerationResult Run(string root, string outRoot, string pin, int seed, int lines, string folder) =>
        new Pipeline().Run(new GenerationConfig { Discrepancy = pin, Seed = seed, LinesQuantity = lines, OutputFolder = Path.Combine(outRoot, folder), OutputFormat = "html" }, root);

    /// <summary>The deterministic compare the lesson's script implements: parties, tax, lines by position, totals.</summary>
    private static bool FieldIdentical(Invoice inv, PurchaseOrder po, out string why)
    {
        why = null;
        if (inv.PoNumber != po.PoNumber) { why = "poNumber"; return false; }
        if (inv.Currency != po.Currency) { why = "currency"; return false; }
        foreach (var (a, b, name) in new[] { (inv.Vendor, po.Vendor, "vendor"), (inv.Buyer, po.Buyer, "buyer") })
            if (a.Name != b.Name || a.Address != b.Address || a.Country != b.Country || a.TaxId != b.TaxId) { why = name; return false; }
        if (inv.TaxName != po.TaxName || inv.TaxRate != po.TaxRate) { why = "tax"; return false; }
        if (inv.Lines.Count != po.Lines.Count) { why = "line count"; return false; }
        for (var i = 0; i < inv.Lines.Count; i++)
        {
            var a = inv.Lines[i]; var b = po.Lines[i];
            if (a.Description != b.Description || a.Quantity != b.Quantity || a.UnitPrice != b.UnitPrice || a.LineTotal != b.LineTotal) { why = $"line {i + 1}"; return false; }
        }
        if (inv.Subtotal != po.Subtotal || inv.TaxTotal != po.TaxTotal || inv.Total != po.Total) { why = "totals"; return false; }
        return true;
    }

    private static int StableHash(string text) { unchecked { var h = 17; foreach (var c in text) h = h * 31 + c; return h; } }
}
