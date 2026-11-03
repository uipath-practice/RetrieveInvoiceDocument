using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace InvoiceGenerator.Models
{
    /// <summary>A company as it appears on a document. The same class serves vendor and buyer.</summary>
    public class Party
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Country { get; set; }
        public string TaxId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Contact { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Phone { get; set; }

        public Party Clone() => (Party)MemberwiseClone();
    }

    /// <summary>One line on either document. <see cref="DeliveryDate"/> and <see cref="Material"/> are purchase-order only.</summary>
    public class Line
    {
        public int N { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string DeliveryDate { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string Material { get; set; }
        /// <summary>Unit of measure as SAP prints it (EA, PC, BOX). Never compared; PO rendering only.</summary>
        [JsonIgnore] public string Unit { get; set; }

        public Line Clone() => (Line)MemberwiseClone();
        public void Recalculate() => LineTotal = Money.Round(Quantity * UnitPrice);
    }

    /// <summary>What the two documents share: parties, lines, tax and totals. Totals are always derived from the lines.</summary>
    public abstract class Document
    {
        public string Currency { get; set; }
        public Party Vendor { get; set; }
        public Party Buyer { get; set; }
        public List<Line> Lines { get; set; } = new List<Line>();
        public decimal Subtotal { get; set; }
        public string TaxName { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal Total { get; set; }

        public void Recalculate()
        {
            for (var i = 0; i < Lines.Count; i++) { Lines[i].N = i + 1; Lines[i].Recalculate(); }
            Subtotal = Money.Round(Lines.Sum(l => l.LineTotal));
            TaxTotal = Money.Round(Subtotal * TaxRate / 100m);
            Total = Money.Round(Subtotal + TaxTotal);
        }
    }

    public class PurchaseOrder : Document
    {
        public string PoNumber { get; set; }
        public string PoDate { get; set; }
    }

    public class Invoice : Document
    {
        public string InvoiceNumber { get; set; }
        public string PoNumber { get; set; }
        public string InvoiceDate { get; set; }
        public string DueDate { get; set; }
        public string Notes { get; set; }

        /// <summary>A deep copy so an injector can edit the invoice while the purchase order stays the truth.</summary>
        public static Invoice From(PurchaseOrder po, string invoiceNumber, string invoiceDate, string dueDate, string notes)
        {
            var inv = new Invoice
            {
                InvoiceNumber = invoiceNumber, PoNumber = po.PoNumber, InvoiceDate = invoiceDate, DueDate = dueDate, Notes = notes,
                Currency = po.Currency, Vendor = po.Vendor.Clone(), Buyer = po.Buyer.Clone(),
                TaxName = po.TaxName, TaxRate = po.TaxRate,
                Lines = po.Lines.Select(l => { var c = l.Clone(); c.DeliveryDate = null; c.Material = null; return c; }).ToList()
            };
            inv.Recalculate();
            return inv;
        }
    }

    public static class Money
    {
        public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
