using System.Collections.Generic;
using System.Linq;
using System.Text;
using InvoiceGenerator.Core;
using InvoiceGenerator.Models;
using static InvoiceGenerator.Core.Formatting;

namespace InvoiceGenerator.Render
{
    /// <summary>
    /// Fills <c>Templates/invoice.html</c>. The header band is coloured by the expected outcome — dark green,
    /// salmon, dark red — as a hint for the human in the validation station (decided 2026-11-03); the agent only
    /// ever sees JSON, so it cannot read it.
    /// </summary>
    public static class InvoiceRenderer
    {
        public static string HeaderColour(string status) =>
            status == Status.Red ? "darkred" : status == Status.Yellow ? "darksalmon" : "darkgreen";

        public static string Render(TemplateRenderer renderer, string template, Invoice inv, string status)
        {
            var rows = new StringBuilder();
            foreach (var l in inv.Lines)
                rows.Append("<tr><td class=\"d\">").Append(Html(l.Description)).Append("</td><td class=\"n\">").Append(l.Quantity)
                    .Append("</td><td class=\"n\">").Append(Amount(l.UnitPrice)).Append("</td><td class=\"n\">").Append(Amount(l.LineTotal)).Append("</td></tr>\n");

            return renderer.Render(template, new Dictionary<string, string>
            {
                ["HEADER_COLOR"] = HeaderColour(status),
                ["VENDOR_NAME"] = Html(inv.Vendor.Name), ["VENDOR_ADDRESS"] = Html(inv.Vendor.Address), ["VENDOR_COUNTRY"] = Html(inv.Vendor.Country),
                ["VENDOR_TAXID"] = Html(inv.Vendor.TaxId), ["VENDOR_CONTACT"] = Html(inv.Vendor.Contact), ["VENDOR_PHONE"] = Html(inv.Vendor.Phone),
                ["BUYER_NAME"] = Html(inv.Buyer.Name), ["BUYER_ADDRESS"] = Html(inv.Buyer.Address), ["BUYER_COUNTRY"] = Html(inv.Buyer.Country), ["BUYER_TAXID"] = Html(inv.Buyer.TaxId),
                ["INVOICE_NUMBER"] = Html(inv.InvoiceNumber), ["INVOICE_DATE"] = Long(inv.InvoiceDate), ["DUE_DATE"] = Long(inv.DueDate), ["PO_NUMBER"] = Html(inv.PoNumber),
                ["CURRENCY"] = Html(inv.Currency), ["LINE_ROWS"] = rows.ToString(),
                ["SUBTOTAL"] = Money(inv.Subtotal, inv.Currency), ["TAX_LABEL"] = $"{Html(inv.TaxName)} {Percent(inv.TaxRate)}", ["TAX_TOTAL"] = Money(inv.TaxTotal, inv.Currency),
                ["TOTAL"] = Money(inv.Total, inv.Currency), ["NOTES"] = Html(inv.Notes)
            });
        }
    }

    /// <summary>
    /// Fills <c>Templates/purchase-order-sap.html</c>: the purchase order as a read-only SAP ME23N screen — vendor
    /// block, organisational data, the item overview table. It only has to look like where the data comes from.
    /// </summary>
    public static class PurchaseOrderRenderer
    {
        public static string Render(TemplateRenderer renderer, string template, PurchaseOrder po, System.DateTime today)
        {
            var rows = new StringBuilder();
            foreach (var l in po.Lines)
                rows.Append("<tr><td class=\"n\">").Append(l.N * 10).Append("</td><td>").Append(Html(l.Material)).Append("</td><td class=\"t\">").Append(Html(l.Description))
                    .Append("</td><td class=\"n\">").Append(Amount(l.Quantity)).Append("</td><td>").Append(Html(l.Unit ?? "EA")).Append("</td><td>D</td><td>").Append(Sap(l.DeliveryDate))
                    .Append("</td><td class=\"n\">").Append(Amount(l.UnitPrice)).Append("</td><td>").Append(Html(po.Currency)).Append("</td><td class=\"n\">1</td><td>").Append(Html(l.Unit ?? "EA"))
                    .Append("</td><td class=\"n\">").Append(Amount(l.LineTotal)).Append("</td></tr>\n");

            var vendorNumber = "10" + new string(po.Vendor.TaxId.Where(char.IsDigit).Take(5).ToArray()).PadLeft(5, '0');
            return renderer.Render(template, new Dictionary<string, string>
            {
                ["PO_NUMBER"] = Html(po.PoNumber), ["PO_DATE"] = Sap(po.PoDate), ["TODAY"] = Sap(today),
                ["VENDOR_NUMBER"] = vendorNumber, ["VENDOR_NAME"] = Html(po.Vendor.Name), ["VENDOR_ADDRESS"] = Html(po.Vendor.Address), ["VENDOR_COUNTRY"] = Html(po.Vendor.Country),
                ["VENDOR_TAXID"] = Html(po.Vendor.TaxId), ["VENDOR_CONTACT"] = Html(po.Vendor.Contact), ["VENDOR_PHONE"] = Html(po.Vendor.Phone),
                ["BUYER_NAME"] = Html(po.Buyer.Name), ["BUYER_ADDRESS"] = Html(po.Buyer.Address), ["BUYER_COUNTRY"] = Html(po.Buyer.Country), ["BUYER_TAXID"] = Html(po.Buyer.TaxId),
                ["CURRENCY"] = Html(po.Currency), ["ITEM_ROWS"] = rows.ToString(), ["ITEM_COUNT"] = po.Lines.Count.ToString(),
                ["NET_VALUE"] = Amount(po.Subtotal), ["TAX_LABEL"] = $"{Html(po.TaxName)} {Percent(po.TaxRate)}", ["TAX_VALUE"] = Amount(po.TaxTotal), ["GROSS_VALUE"] = Amount(po.Total)
            });
        }
    }
}
