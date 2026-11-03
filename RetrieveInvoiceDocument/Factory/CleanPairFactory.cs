using System.Linq;
using InvoiceGenerator.Core;
using InvoiceGenerator.Data;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Factory
{
    /// <summary>
    /// The clean truth: a purchase order, and an invoice that is field-identical to it on everything the
    /// deterministic compare looks at — parties, lines (description, quantity, unit price), tax, totals.
    /// Every difference a run carries is one injector applied afterwards; a clean pair is this with nothing done to it.
    /// </summary>
    public static class CleanPairFactory
    {
        public static (PurchaseOrder Po, Invoice Invoice) Build(Identity id)
        {
            var po = new PurchaseOrder
            {
                PoNumber = id.PoNumber,
                PoDate = Formatting.Iso(id.PoDate),
                Currency = id.Seller.Currency,
                Vendor = Party(id.Seller),
                Buyer = Party(id.Buyer),
                TaxName = id.Seller.TaxName,
                TaxRate = id.Seller.TaxRate,
                Lines = id.Products.Select((p, i) => new Line
                {
                    N = i + 1,
                    Description = p.Name,
                    Quantity = id.Quantities[i],
                    UnitPrice = p.Price,
                    DeliveryDate = Formatting.Iso(id.Today.AddDays(id.DeliveryOffsets[i])),
                    Material = id.Materials[i],
                    Unit = p.Qty >= 10 ? "PC" : p.Qty >= 5 ? "PC" : "EA"
                }).ToList()
            };
            po.Recalculate();

            var invoice = Invoice.From(po, id.InvoiceNumber, Formatting.Iso(id.InvoiceDate), Formatting.Iso(id.DueDate),
                "Payment due within 30 days. Thank you for your business.");
            return (po, invoice);
        }

        public static Party Party(Company c) => new Party
        {
            Name = c.Name, Address = c.Address, Country = c.Country, TaxId = c.TaxCode, Contact = c.ContactName, Phone = c.ContactPhone
        };
    }
}
