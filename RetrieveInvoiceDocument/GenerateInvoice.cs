using System;
using InvoiceGenerator.Core;
using InvoiceGenerator.Models;
using UiPath.CodedWorkflows;

namespace RetrieveInvoiceDocument
{
    public class GenerateInvoice : CodedWorkflow
    {
        /// <param name="in_Discrepancy">
        /// A discrepancy id to plant: VENDOR_MISMATCH, TAX_RATE_MISMATCH, EXTRA_LINE_PACKAGING, EXTRA_LINE_UNORDERED, DESCRIPTION_SYNONYM,
        /// DESCRIPTION_DIFFERENT_ITEM (the six the probability draw uses), FORMATTING_ONLY (green) or PO_NUMBER_UNKNOWN (red; never drawn);
        /// other catalogue ids exist for the course's tests only. "none" = a clean pair. Empty draws by probability;
        /// an unknown id fails the job rather than falling back to random.
        /// </param>
        /// <param name="in_DiscrepancyProbability">0–100: the chance an unpinned invoice carries one discrepancy from the exercise set. Default 20.</param>
        /// <param name="in_Seed">Non-zero reproduces the same invoice and purchase order; zero draws a fresh seed, recorded in the answer key.</param>
        /// <param name="in_LinesQuantity">Lines on the purchase order, 2–8. Default 4.</param>
        [Workflow]
        public (string out_InvoiceFileName, string out_InvoiceNumber, string out_PONumber, string out_ExpectedStatus, string out_Discrepancy,
                string out_InvoicePath, string out_AnswerPath, string out_PurchaseOrderJson, string out_PurchaseOrderHtmlPath) Execute(
            string in_Discrepancy = "",
            int in_DiscrepancyProbability = 20,
            int in_Seed = 0,
            int in_LinesQuantity = 4)
        {
            var config = new GenerationConfig
            {
                Discrepancy = in_Discrepancy ?? "", DiscrepancyProbability = in_DiscrepancyProbability, Seed = in_Seed, LinesQuantity = in_LinesQuantity,
                OutputFolder = "Output", OutputFormat = "pdf"
            };
            var result = new Pipeline().Run(config, Pipeline.FindProjectRoot());
            var m = result.Manifest;
            Log($"Generated {m.InvoiceFileName} for {m.PoNumber} (seed {m.Seed}, {m.Discrepancy ?? "clean"}, expected {m.Expected.Status})");
            return (m.InvoiceFileName, m.InvoiceNumber, m.PoNumber, m.Expected.Status, m.Discrepancy ?? "",
                    result.InvoiceFile, result.AnswerFile, result.PurchaseOrderJson, result.PurchaseOrderHtmlFile);
        }
    }
}
