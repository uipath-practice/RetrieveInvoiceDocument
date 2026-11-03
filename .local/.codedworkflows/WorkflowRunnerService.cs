using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UiPath.CodedWorkflows;
using UiPath.CodedWorkflows.Interfaces;
using UiPath.Activities.Contracts;
using RetrieveInvoiceDocument;
using InvoiceGenerator.Core;
using InvoiceGenerator.Models;
using InvoicePDFGenerator;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

[assembly: WorkflowRunnerServiceAttribute(typeof(RetrieveInvoiceDocument.WorkflowRunnerService))]
namespace RetrieveInvoiceDocument
{
    public class WorkflowRunnerService
    {
        private readonly ICodedWorkflowServices _services;
        public WorkflowRunnerService(ICodedWorkflowServices services)
        {
            _services = services;
        }

        /// <param name="in_Discrepancy">
		/// Optional. Pins one discrepancy by id (VENDOR_MISMATCH, TAX_RATE_MISMATCH, EXTRA_LINE_PACKAGING, EXTRA_LINE_UNORDERED,
		/// DESCRIPTION_SYNONYM, DESCRIPTION_DIFFERENT_ITEM, FORMATTING_ONLY, SPLIT_LINES_VALID, SPLIT_LINES_INVALID, PRICE_WITHIN_TOLERANCE,
		/// QUANTITY_OVERBILLED, PARTIAL_DELIVERY, PO_NUMBER_UNKNOWN, CURRENCY_MISMATCH) or "none" for a clean pair. Empty draws by probability;
		/// an unknown id fails the job rather than falling back to random.
		/// </param>
		/// <param name="in_DiscrepancyProbability">0–100: the chance an unpinned invoice carries one discrepancy from the exercise set. Default 20.</param>
		/// <param name="in_Seed">Non-zero reproduces the same invoice and purchase order; zero draws a fresh seed, recorded in the answer key.</param>
		/// <param name="in_LinesQuantity">Lines on the purchase order, 2–8. Default 4.</param>
		/// <param name="isolated">Indicates whether to isolate executions (run them within a different process)</param>
        public (System.String out_InvoiceFileName, System.String out_InvoiceNumber, System.String out_PONumber, System.String out_ExpectedStatus, System.String out_Discrepancy, System.String out_InvoicePath, System.String out_AnswerPath, System.String out_PurchaseOrderJson, System.String out_PurchaseOrderHtmlPath) GenerateInvoice(System.String in_Discrepancy = "", System.Int32 in_DiscrepancyProbability = 20, System.Int32 in_Seed = 0, System.Int32 in_LinesQuantity = 4, System.Boolean isolated = false)
        {
            var result = _services.WorkflowInvocationService.RunWorkflow(@"RetrieveInvoiceDocument/GenerateInvoice.cs", new Dictionary<string, object> { { "in_Discrepancy", in_Discrepancy }, { "in_DiscrepancyProbability", in_DiscrepancyProbability }, { "in_Seed", in_Seed }, { "in_LinesQuantity", in_LinesQuantity } }, default, isolated, default, GetAssemblyName());
            return ((System.String)result["out_InvoiceFileName"], (System.String)result["out_InvoiceNumber"], (System.String)result["out_PONumber"], (System.String)result["out_ExpectedStatus"], (System.String)result["out_Discrepancy"], (System.String)result["out_InvoicePath"], (System.String)result["out_AnswerPath"], (System.String)result["out_PurchaseOrderJson"], (System.String)result["out_PurchaseOrderHtmlPath"]);
        }

        /// <summary>
        /// Invokes the GenerateJSONs.cs
        /// </summary>
        /// <param name="isolated">Indicates whether to isolate executions (run them within a different process)</param>
        public void GenerateJSONs(System.Int32 in_LinesQuantity = 4, System.Int32 in_FailureProbability = 0, System.String in_InvoiceID = "001", System.String in_POID = "001", System.Boolean isolated = false)
        {
            var result = _services.WorkflowInvocationService.RunWorkflow(@"GenerateJSONs.cs", new Dictionary<string, object> { { "in_LinesQuantity", in_LinesQuantity }, { "in_FailureProbability", in_FailureProbability }, { "in_InvoiceID", in_InvoiceID }, { "in_POID", in_POID } }, default, isolated, default, GetAssemblyName());
        }

        /// <summary>
        /// Invokes the Main.xaml
        /// </summary>
        /// <param name="isolated">Indicates whether to isolate executions (run them within a different process)</param>
        public (UiPath.Platform.ResourceHandling.IResource out_InvoicePDF, string out_InvoiceFileName) Main(int in_FailureProbability, System.Boolean isolated = false)
        {
            var result = _services.WorkflowInvocationService.RunWorkflow(@"Main.xaml", new Dictionary<string, object> { { "in_FailureProbability", in_FailureProbability } }, default, isolated, default, GetAssemblyName());
            return ((UiPath.Platform.ResourceHandling.IResource)result["out_InvoicePDF"], (string)result["out_InvoiceFileName"]);
        }

        /// <summary>
        /// Invokes the PDFWriter.cs
        /// </summary>
        /// <param name="isolated">Indicates whether to isolate executions (run them within a different process)</param>
        public void PDFWriter(System.Boolean isolated = false)
        {
            var result = _services.WorkflowInvocationService.RunWorkflow(@"PDFWriter.cs", new Dictionary<string, object> { }, default, isolated, default, GetAssemblyName());
        }

        private string GetAssemblyName()
        {
            var assemblyProvider = _services.Container.Resolve<ILibraryAssemblyProvider>();
            return assemblyProvider.GetLibraryAssemblyName(GetType().Assembly);
        }
    }
}