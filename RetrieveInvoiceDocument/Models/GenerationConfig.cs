namespace InvoiceGenerator.Models
{
    /// <summary>What the caller asked for. Everything derived from it lives on the identity or in the manifest.</summary>
    public class GenerationConfig
    {
        /// <summary>An injector id to pin, or empty to draw by <see cref="DiscrepancyProbability"/>. Unknown ids fail the run.</summary>
        public string Discrepancy { get; set; } = "";

        /// <summary>0–100. The chance that an unpinned run carries one discrepancy drawn from the exercise set. Default 20.</summary>
        public int DiscrepancyProbability { get; set; } = 20;

        /// <summary>Non-zero reproduces a pair exactly (dates excepted — they follow today). Zero draws a fresh seed, recorded in the manifest.</summary>
        public int Seed { get; set; }

        /// <summary>Lines on the purchase order, 2–8. One is clamped up to two so every injector has something to work on.</summary>
        public int LinesQuantity { get; set; } = 4;

        public string OutputFolder { get; set; } = "Output";

        /// <summary><c>pdf</c>, or <c>html</c> to skip the PDF conversion (the smoke test).</summary>
        public string OutputFormat { get; set; } = "pdf";
    }
}
