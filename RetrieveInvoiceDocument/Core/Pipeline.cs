using System;
using System.IO;
using System.Text.Json;
using InvoiceGenerator.Data;
using InvoiceGenerator.Discrepancies;
using InvoiceGenerator.Factory;
using InvoiceGenerator.Models;
using InvoiceGenerator.Render;

namespace InvoiceGenerator.Core
{
    /// <summary>What one run produced: the documents in memory and the files on disk that Main.xaml uploads.</summary>
    public class GenerationResult
    {
        public Manifest Manifest { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; }
        public Invoice Invoice { get; set; }
        public string InvoiceFile { get; set; }
        public string AnswerFile { get; set; }
        public string InvoiceJsonFile { get; set; }
        public string PurchaseOrderJsonFile { get; set; }
        public string PurchaseOrderHtmlFile { get; set; }
        public string PurchaseOrderJson { get; set; }
    }

    /// <summary>
    /// Clean pair → one planted discrepancy (or none) → render → write. The factory never knows which
    /// discrepancy runs, so a clean pair is the truth with nothing done to it, and every difference the
    /// documents carry is named in the answer key.
    /// </summary>
    public sealed class Pipeline
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public GenerationResult Run(GenerationConfig config, string projectRoot)
        {
            var catalog = new Catalog(Path.Combine(projectRoot, "Data"));
            var seed = config.Seed != 0 ? config.Seed : new Random().Next(1, int.MaxValue);
            var identity = new Identity(catalog, seed, config.LinesQuantity);
            var (po, invoice) = CleanPairFactory.Build(identity);

            var engine = Catalogue.Default();
            var injector = engine.Choose(config.Discrepancy, config.DiscrepancyProbability, identity.Stream("discrepancy"));
            var ctx = new InjectionContext(invoice, po, identity, catalog, identity.Stream("inject"));
            var expected = engine.Apply(injector, ctx);

            var renderer = new TemplateRenderer();
            var invoiceHtml = InvoiceRenderer.Render(renderer, File.ReadAllText(Path.Combine(projectRoot, "Templates", "invoice.html")), invoice, expected.Status);
            var poHtml = PurchaseOrderRenderer.Render(renderer, File.ReadAllText(Path.Combine(projectRoot, "Templates", "purchase-order-sap.html")), po, identity.Today);

            Directory.CreateDirectory(config.OutputFolder);
            var asHtml = string.Equals(config.OutputFormat, "html", StringComparison.OrdinalIgnoreCase);
            var invoiceFile = Path.Combine(config.OutputFolder, invoice.InvoiceNumber + (asHtml ? ".html" : ".pdf"));
            if (asHtml) File.WriteAllText(invoiceFile, invoiceHtml);
            else new PdfGenerator().Convert(invoiceHtml, invoiceFile);

            var manifest = new Manifest
            {
                InvoiceNumber = invoice.InvoiceNumber, PoNumber = po.PoNumber, InvoiceFileName = Path.GetFileName(invoiceFile),
                Seed = seed, LinesQuantity = identity.LinesQuantity,
                Discrepancy = injector?.Id, DiscrepancyDescription = injector?.Description, Expected = expected,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var result = new GenerationResult
            {
                Manifest = manifest, PurchaseOrder = po, Invoice = invoice, InvoiceFile = invoiceFile,
                AnswerFile = Path.Combine(config.OutputFolder, invoice.InvoiceNumber + "-answer.json"),
                InvoiceJsonFile = Path.Combine(config.OutputFolder, invoice.InvoiceNumber + ".json"),
                PurchaseOrderJsonFile = Path.Combine(config.OutputFolder, po.PoNumber + ".json"),
                PurchaseOrderHtmlFile = Path.Combine(config.OutputFolder, po.PoNumber + ".html"),
                PurchaseOrderJson = JsonSerializer.Serialize(po, Json)
            };
            File.WriteAllText(result.AnswerFile, JsonSerializer.Serialize(manifest, Json));
            File.WriteAllText(result.InvoiceJsonFile, JsonSerializer.Serialize(invoice, Json));
            File.WriteAllText(result.PurchaseOrderJsonFile, result.PurchaseOrderJson);
            File.WriteAllText(result.PurchaseOrderHtmlFile, poHtml);

            Console.WriteLine($"{invoice.InvoiceNumber} against {po.PoNumber}: {identity.Seller.Name} → {identity.Buyer.Name}, {identity.LinesQuantity} line(s), seed {seed}, " +
                              (injector == null ? "clean — expected green at the deterministic gate" : $"{injector.Id} — expected {expected.Status}"));
            return result;
        }

        /// <summary>The folder holding <c>Templates/</c> and <c>Data/</c>: the working directory or a few levels up (bin/ during local runs).</summary>
        public static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (var level = 0; level < 5 && dir != null; level++, dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Templates", "invoice.html"))) return dir.FullName;
            throw new DirectoryNotFoundException($"No Templates/invoice.html at or above {Directory.GetCurrentDirectory()}.");
        }
    }
}
