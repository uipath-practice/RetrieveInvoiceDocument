using System;
using System.Collections.Generic;
using InvoiceGenerator.Core;
using InvoiceGenerator.Data;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Discrepancies
{
    /// <summary>
    /// One deliberately planted difference between the invoice and the purchase order. An injector edits the
    /// invoice only — the purchase order is the truth the buyer holds — and records what it did as the findings
    /// the agent is expected to report. One injector, one change, one colour.
    /// </summary>
    public interface IInjector
    {
        /// <summary>Stable id, UPPER_SNAKE, quoted by the lesson and by tests: <c>VENDOR_MISMATCH</c>.</summary>
        string Id { get; }

        /// <summary>What the agent must say: <see cref="Status.Green"/>, <see cref="Status.Yellow"/> or <see cref="Status.Red"/>.</summary>
        string Expected { get; }

        /// <summary>One line, in the exercise's words. Ends up in scenarios.md and in the answer key.</summary>
        string Description { get; }

        /// <summary>Whether a random draw may pick it. The six exercise discrepancies are; the rest exist for tests and pins.</summary>
        bool InExerciseSet { get; }

        void Inject(InjectionContext ctx);
    }

    public sealed class InjectionContext
    {
        public InjectionContext(Invoice invoice, PurchaseOrder po, Identity identity, Catalog catalog, Random rng)
        {
            Invoice = invoice; Po = po; Identity = identity; Catalog = catalog; Rng = rng;
        }

        public Invoice Invoice { get; }
        public PurchaseOrder Po { get; }
        public Identity Identity { get; }
        public Catalog Catalog { get; }
        public Random Rng { get; }
        public List<Finding> Findings { get; } = new List<Finding>();

        public Finding Add(string area, string field, string severity, string invoiceValue, string poValue, string note, int? invoiceLine = null, int? poLine = null)
        {
            var f = new Finding { Area = area, Field = field, Severity = severity, InvoiceValue = invoiceValue, PoValue = poValue, Note = note, InvoiceLine = invoiceLine, PoLine = poLine };
            Findings.Add(f);
            return f;
        }
    }
}
