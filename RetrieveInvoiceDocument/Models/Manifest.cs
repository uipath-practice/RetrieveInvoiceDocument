using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InvoiceGenerator.Models
{
    /// <summary>
    /// The answer key of one run, written beside the invoice as <c>INV-&lt;n&gt;-answer.json</c>.
    /// No participant solution reads it; tests and trainers do. The three colours are the exercise's
    /// contract: <c>green</c> pays, <c>yellow</c> is a presentation difference a reviewer waves through,
    /// <c>red</c> is a real discrepancy.
    /// </summary>
    public class Manifest
    {
        public string InvoiceNumber { get; set; }
        public string PoNumber { get; set; }
        public string InvoiceFileName { get; set; }
        public int Seed { get; set; }
        public int LinesQuantity { get; set; }
        /// <summary>The injector applied, or null for a clean pair.</summary>
        public string Discrepancy { get; set; }
        public string DiscrepancyDescription { get; set; }
        public ExpectedOutcome Expected { get; set; }
        public string GeneratedAt { get; set; }
    }

    public class ExpectedOutcome
    {
        /// <summary><c>green</c> | <c>yellow</c> | <c>red</c>.</summary>
        public string Status { get; set; }
        /// <summary>Whether the deterministic compare must pass (only a clean pair does).</summary>
        public bool DeterministicMatch { get; set; }
        public List<Finding> Findings { get; set; } = new List<Finding>();
    }

    /// <summary>The same shape the agent returns in <c>matchResult.findings</c> (the exercise contract).</summary>
    public class Finding
    {
        public string Area { get; set; }
        public string Field { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? InvoiceLine { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PoLine { get; set; }
        public string Severity { get; set; }
        public string InvoiceValue { get; set; }
        public string PoValue { get; set; }
        public string Note { get; set; }
    }

    public static class Status
    {
        public const string Green = "green";
        public const string Yellow = "yellow";
        public const string Red = "red";
    }
}
