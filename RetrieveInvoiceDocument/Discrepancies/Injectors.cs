using System;
using System.Collections.Generic;
using System.Linq;
using InvoiceGenerator.Core;
using InvoiceGenerator.Data;
using InvoiceGenerator.Models;

namespace InvoiceGenerator.Discrepancies
{
    /// <summary>The catalogue, in the order scenarios.md lists it. The six exercise discrepancies first.</summary>
    public static class Catalogue
    {
        public static Engine Default() => new Engine(new IInjector[]
        {
            new VendorMismatch(), new TaxRateMismatch(), new ExtraLinePackaging(), new ExtraLineUnordered(),
            new DescriptionSynonym(), new DescriptionDifferentItem(),
            new FormattingOnly(), new SplitLinesValid(), new SplitLinesInvalid(), new PriceWithinTolerance(),
            new QuantityOverbilled(), new PartialDelivery(), new PoNumberUnknown(), new CurrencyMismatch()
        });
    }

    internal static class Pick
    {
        public static Line Line(InjectionContext ctx, Func<Line, bool> where)
        {
            var ok = ctx.Invoice.Lines.Where(where).ToList();
            if (ok.Count == 0) ok = ctx.Invoice.Lines.ToList();
            return ok[ctx.Rng.Next(ok.Count)];
        }

        public static Product ProductOf(InjectionContext ctx, Line line) =>
            ctx.Identity.Products.FirstOrDefault(p => p.Name == line.Description) ?? ctx.Identity.Products[0];

        public static Product OtherProduct(InjectionContext ctx)
        {
            var used = new HashSet<string>(ctx.Po.Lines.Select(l => l.Description));
            var pool = ctx.Catalog.Products.Where(p => !used.Contains(p.Name)).ToList();
            return pool[ctx.Rng.Next(pool.Count)];
        }

        public static Company OtherCompany(InjectionContext ctx, Func<Company, bool> prefer)
        {
            var pool = ctx.Catalog.Companies.Where(c => c.Name != ctx.Po.Vendor.Name && c.Name != ctx.Po.Buyer.Name).ToList();
            var preferred = pool.Where(prefer).ToList();
            var from = preferred.Count > 0 ? preferred : pool;
            return from[ctx.Rng.Next(from.Count)];
        }

        /// <summary>After the invoice is recalculated: one finding on the grand total when it no longer equals the PO's.</summary>
        public static void TotalFinding(InjectionContext ctx, string severity, string note)
        {
            ctx.Invoice.Recalculate();
            if (ctx.Invoice.Total == ctx.Po.Total) return;
            ctx.Add("total", "total", severity, Formatting.Money(ctx.Invoice.Total, ctx.Invoice.Currency), Formatting.Money(ctx.Po.Total, ctx.Po.Currency), note);
        }

        public static string Money(Document d, decimal v) => Formatting.Money(v, d.Currency);
    }

    // ------------------------------------------------------------------ the exercise set (★)

    /// <summary>Another company's name, address or tax id on the invoice — one, two or all three.</summary>
    public sealed class VendorMismatch : IInjector
    {
        public string Id => "VENDOR_MISMATCH";
        public string Expected => Status.Red;
        public string Description => "The invoice names another vendor: a different company name, address or tax id (one or more), amounts unchanged.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var v = ctx.Invoice.Vendor; var truth = ctx.Po.Vendor;
            switch (ctx.Rng.Next(3))
            {
                case 0: // a whole other company, same currency where possible so nothing else looks off
                    var other = Pick.OtherCompany(ctx, c => c.Currency == ctx.Po.Currency);
                    v.Name = other.Name; v.Address = other.Address; v.Country = other.Country; v.TaxId = other.TaxCode; v.Contact = other.ContactName; v.Phone = other.ContactPhone;
                    ctx.Add("vendor", "name", Status.Red, v.Name, truth.Name, "the invoice is issued by a different company than the purchase order was sent to");
                    ctx.Add("vendor", "address", Status.Red, v.Address, truth.Address, "vendor address differs");
                    ctx.Add("vendor", "taxId", Status.Red, v.TaxId, truth.TaxId, "vendor tax id differs");
                    break;
                case 1: // only the tax id: two digits swapped
                    v.TaxId = SwapDigits(truth.TaxId, ctx.Rng);
                    ctx.Add("vendor", "taxId", Status.Red, v.TaxId, truth.TaxId, "the vendor's tax registration number does not match the purchase order");
                    break;
                default: // only the name: a namesake with the same address
                    var name = Pick.OtherCompany(ctx, c => c.Country == truth.Country).Name;
                    v.Name = name;
                    ctx.Add("vendor", "name", Status.Red, v.Name, truth.Name, "a different company name at the purchase order's vendor address");
                    break;
            }
        }

        private static string SwapDigits(string id, Random rng)
        {
            var chars = id.ToCharArray();
            var digits = Enumerable.Range(0, chars.Length).Where(i => char.IsDigit(chars[i])).ToList();
            if (digits.Count < 2) return id + "1";
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var a = digits[rng.Next(digits.Count)]; var b = digits[rng.Next(digits.Count)];
                if (chars[a] == chars[b]) continue;
                (chars[a], chars[b]) = (chars[b], chars[a]);
                return new string(chars);
            }
            chars[digits[0]] = chars[digits[0]] == '9' ? '0' : (char)(chars[digits[0]] + 1);
            return new string(chars);
        }
    }

    /// <summary>A different tax name and rate; lines unchanged, so tax and total differ.</summary>
    public sealed class TaxRateMismatch : IInjector
    {
        public string Id => "TAX_RATE_MISMATCH";
        public string Expected => Status.Red;
        public string Description => "The invoice applies a different tax (name and rate) than the purchase order; every line matches, the tax and total do not.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var other = Pick.OtherCompany(ctx, c => c.TaxRate != ctx.Po.TaxRate && c.TaxName != ctx.Po.TaxName);
            if (other.TaxRate == ctx.Po.TaxRate) other = Pick.OtherCompany(ctx, c => c.TaxRate != ctx.Po.TaxRate);
            ctx.Invoice.TaxName = other.TaxName; ctx.Invoice.TaxRate = other.TaxRate;
            ctx.Add("tax", "taxRate", Status.Red, $"{ctx.Invoice.TaxName} {Formatting.Percent(ctx.Invoice.TaxRate)}", $"{ctx.Po.TaxName} {Formatting.Percent(ctx.Po.TaxRate)}", "the tax rate on the invoice is not the one agreed on the purchase order");
            Pick.TotalFinding(ctx, Status.Red, "the total differs because of the tax rate");
        }
    }

    /// <summary>One PO line billed as item + packaging, same line total, same invoice total.</summary>
    public sealed class ExtraLinePackaging : IInjector
    {
        public string Id => "EXTRA_LINE_PACKAGING";
        public string Expected => Status.Yellow;
        public string Description => "One purchase-order line is billed as two: the item at 80 % of the price plus a packaging line at 20 %; the line total and the invoice total still equal the purchase order.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var line = Pick.Line(ctx, l => l.UnitPrice >= 0.5m);
            var poLine = line.N; var full = line.UnitPrice;
            var main = Money.Round(full * 0.8m); var pack = Money.Round(full - main);
            line.UnitPrice = main;
            var packaging = new Line { Description = $"{line.Description} packaging", Quantity = line.Quantity, UnitPrice = pack };
            ctx.Invoice.Lines.Insert(ctx.Invoice.Lines.IndexOf(line) + 1, packaging);
            ctx.Invoice.Recalculate();
            ctx.Add("line", "unitPrice", Status.Yellow, Pick.Money(ctx.Invoice, main), Pick.Money(ctx.Po, full), "the unit price is split between the item and a packaging line", line.N, poLine);
            ctx.Add("line", "", Status.Yellow, $"{packaging.Description} × {packaging.Quantity} @ {Pick.Money(ctx.Invoice, pack)}", "", "packaging line not on the purchase order; together with the item it equals the PO line", packaging.N, null);
        }
    }

    /// <summary>A line for something the buyer never ordered.</summary>
    public sealed class ExtraLineUnordered : IInjector
    {
        public string Id => "EXTRA_LINE_UNORDERED";
        public string Expected => Status.Red;
        public string Description => "The invoice carries one extra line for a product that is not on the purchase order; the invoice total is higher.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var p = Pick.OtherProduct(ctx);
            var extra = new Line { Description = p.Name, Quantity = p.Qty, UnitPrice = p.Price };
            ctx.Invoice.Lines.Add(extra);
            ctx.Invoice.Recalculate();
            ctx.Add("line", "", Status.Red, $"{extra.Description} × {extra.Quantity} @ {Pick.Money(ctx.Invoice, extra.UnitPrice)}", "", "line not on the purchase order", extra.N, null);
            Pick.TotalFinding(ctx, Status.Red, "the invoice total exceeds the purchase order by the extra line");
        }
    }

    /// <summary>Same goods, other words: one or two lines reworded, quantities and prices unchanged.</summary>
    public sealed class DescriptionSynonym : IInjector
    {
        public string Id => "DESCRIPTION_SYNONYM";
        public string Expected => Status.Yellow;
        public string Description => "One or two lines describe the same product in other words (an adjective, an alternative name, a pack size); quantities and prices match.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var count = Math.Min(ctx.Invoice.Lines.Count, ctx.Rng.Next(1, 3));
            foreach (var line in ctx.Invoice.Lines.OrderBy(_ => ctx.Rng.Next()).Take(count).ToList())
            {
                var p = Pick.ProductOf(ctx, line);
                var was = line.Description;
                line.Description = Synonym(p, line.Quantity, ctx.Rng);
                ctx.Add("line", "description", Status.Yellow, line.Description, was, "same item described differently", line.N, line.N);
            }
        }

        public static string Synonym(Product p, int quantity, Random rng)
        {
            var adjective = p.Variations.Count > 0 ? p.Variations[rng.Next(p.Variations.Count)] : "";
            adjective = adjective.Length > 0 ? char.ToUpperInvariant(adjective[0]) + adjective.Substring(1) : adjective;
            var name = string.IsNullOrWhiteSpace(p.Alternative) ? p.Name : p.Alternative;
            switch (rng.Next(3))
            {
                case 0: return $"{adjective} {name}".Trim();
                case 1 when p.Qty > 1: return $"{name} (1 box / {p.Qty} pcs)";
                case 1: return $"{adjective} {name}, single unit".Trim();
                default: return $"{adjective} {p.Name.ToLowerInvariant()}".Trim();
            }
        }
    }

    /// <summary>A different product at the same quantity and price — the numbers hide it.</summary>
    public sealed class DescriptionDifferentItem : IInjector
    {
        public string Id => "DESCRIPTION_DIFFERENT_ITEM";
        public string Expected => Status.Red;
        public string Description => "One line names a different product than the purchase order at the same quantity and unit price, so every total still matches.";
        public bool InExerciseSet => true;

        public void Inject(InjectionContext ctx)
        {
            var line = Pick.Line(ctx, _ => true);
            var was = line.Description;
            line.Description = Pick.OtherProduct(ctx).Name;
            ctx.Add("line", "description", Status.Red, line.Description, was, "a different item at the purchase order's quantity and price", line.N, line.N);
        }
    }

    // ------------------------------------------------------------------ for tests and pins

    /// <summary>Cosmetic differences only: a spelled-out company suffix, a description in capitals.</summary>
    public sealed class FormattingOnly : IInjector
    {
        public string Id => "FORMATTING_ONLY";
        public string Expected => Status.Green;
        public string Description => "Cosmetic differences only — the vendor's legal suffix spelled out and one description in capitals; nothing a reviewer needs to see.";
        public bool InExerciseSet => false;

        private static readonly (string Short, string Long)[] Suffixes =
        {
            ("Pte Ltd", "Pte. Ltd."), ("Pty Ltd", "Pty. Ltd."), ("Inc.", "Incorporated"), ("LLC", "L.L.C."), ("Corp.", "Corporation"),
            ("SRL", "S.R.L."), ("Ltd", "Limited"), ("GmbH", "G.m.b.H.")
        };

        public void Inject(InjectionContext ctx)
        {
            var v = ctx.Invoice.Vendor; var was = v.Name;
            foreach (var (s, l) in Suffixes)
                if (was.EndsWith(" " + s, StringComparison.Ordinal)) { v.Name = was.Substring(0, was.Length - s.Length) + l; break; }
            if (v.Name == was) v.Name = was.ToUpperInvariant();
            ctx.Add("vendor", "name", Status.Green, v.Name, was, "same company, suffix or capitalisation only");

            var line = Pick.Line(ctx, _ => true);
            var d = line.Description;
            line.Description = d.ToUpperInvariant();
            ctx.Add("line", "description", Status.Green, line.Description, d, "same wording in capitals", line.N, line.N);
        }
    }

    /// <summary>One PO line delivered as two or three variants; quantities and amounts add up.</summary>
    public sealed class SplitLinesValid : IInjector
    {
        public string Id => "SPLIT_LINES_VALID";
        public string Expected => Status.Yellow;
        public string Description => "One purchase-order line is invoiced as two or three variant lines (colours) at the same unit price; the quantities and amounts add up to the PO line.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx) => Split.Apply(ctx, valid: true);
    }

    /// <summary>The same split, with one part over-counted so the sum no longer matches.</summary>
    public sealed class SplitLinesInvalid : IInjector
    {
        public string Id => "SPLIT_LINES_INVALID";
        public string Expected => Status.Red;
        public string Description => "One purchase-order line is invoiced as variant lines, but one part carries more units than ordered, so the quantities and the total exceed the PO.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx) => Split.Apply(ctx, valid: false);
    }

    internal static class Split
    {
        private static readonly string[] Colours = { "Red", "Green", "Black", "Blue", "White" };

        public static void Apply(InjectionContext ctx, bool valid)
        {
            var line = Pick.Line(ctx, l => l.Quantity >= 2);
            if (line.Quantity < 2) line.Quantity = 2;   // only when every line is a single unit; the finding records the real PO value
            var poLine = ctx.Po.Lines[line.N - 1];
            var parts = Math.Min(line.Quantity >= 3 ? 3 : 2, Colours.Length);
            var qty = Partition(line.Quantity, parts, ctx.Rng);
            if (!valid) qty[0] += Math.Max(1, line.Quantity / 4);
            var names = Colours.OrderBy(_ => ctx.Rng.Next()).Take(parts).ToList();
            var index = ctx.Invoice.Lines.IndexOf(line);
            ctx.Invoice.Lines.RemoveAt(index);
            var severity = valid ? Status.Yellow : Status.Red;
            for (var i = 0; i < parts; i++)
            {
                var part = new Line { Description = $"{names[i]} {poLine.Description}", Quantity = qty[i], UnitPrice = line.UnitPrice };
                ctx.Invoice.Lines.Insert(index + i, part);
            }
            ctx.Invoice.Recalculate();
            for (var i = 0; i < parts; i++)
            {
                var part = ctx.Invoice.Lines[index + i];
                ctx.Add("line", "quantity", severity, $"{part.Description} × {part.Quantity}", $"{poLine.Description} × {poLine.Quantity}",
                    valid ? $"variant of PO line {poLine.N}; the {parts} variant lines add up to it" : $"variant of PO line {poLine.N}; the variant lines add up to more than it", part.N, poLine.N);
            }
            if (!valid) Pick.TotalFinding(ctx, Status.Red, "the invoice total exceeds the purchase order by the over-counted units");
        }

        private static int[] Partition(int total, int parts, Random rng)
        {
            var cuts = Enumerable.Range(1, total - 1).OrderBy(_ => rng.Next()).Take(parts - 1).OrderBy(x => x).ToList();
            var result = new int[parts]; var prev = 0;
            for (var i = 0; i < cuts.Count; i++) { result[i] = cuts[i] - prev; prev = cuts[i]; }
            result[parts - 1] = total - prev;
            return result;
        }
    }

    /// <summary>One unit price off by at most two percent.</summary>
    public sealed class PriceWithinTolerance : IInjector
    {
        public string Id => "PRICE_WITHIN_TOLERANCE";
        public string Expected => Status.Yellow;
        public string Description => "One unit price differs from the purchase order by half a percent to two percent, up or down; the line and invoice totals follow.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx)
        {
            var line = Pick.Line(ctx, l => l.UnitPrice >= 5m);
            var was = line.UnitPrice;
            var pct = (decimal)(0.5 + ctx.Rng.NextDouble() * 1.5) * (ctx.Rng.Next(2) == 0 ? -1 : 1);
            var now = Money.Round(was * (1 + pct / 100m));
            if (now == was) now = was + 0.05m;
            line.UnitPrice = now;
            ctx.Invoice.Recalculate();
            var diff = Math.Round((now - was) / was * 100m, 2);
            ctx.Add("line", "unitPrice", Status.Yellow, Pick.Money(ctx.Invoice, now), Pick.Money(ctx.Po, was), $"unit price {(diff > 0 ? "+" : "")}{diff}% against the purchase order, within a 2 % tolerance", line.N, line.N);
            Pick.TotalFinding(ctx, Status.Yellow, "the total moves with the unit price, within tolerance");
        }
    }

    /// <summary>More units billed than ordered.</summary>
    public sealed class QuantityOverbilled : IInjector
    {
        public string Id => "QUANTITY_OVERBILLED";
        public string Expected => Status.Red;
        public string Description => "One line bills more units than the purchase order ordered; the line and invoice totals are higher.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx)
        {
            var line = Pick.Line(ctx, _ => true);
            var p = Pick.ProductOf(ctx, line);
            var was = line.Quantity;
            line.Quantity += Math.Max(1, p.Qty) * ctx.Rng.Next(1, 3);
            ctx.Invoice.Recalculate();
            ctx.Add("line", "quantity", Status.Red, line.Quantity.ToString(), was.ToString(), "more units billed than ordered", line.N, line.N);
            Pick.TotalFinding(ctx, Status.Red, "the invoice total exceeds the purchase order by the extra units");
        }
    }

    /// <summary>Some PO lines not invoiced — a legitimate partial invoice.</summary>
    public sealed class PartialDelivery : IInjector
    {
        public string Id => "PARTIAL_DELIVERY";
        public string Expected => Status.Yellow;
        public string Description => "The invoice covers only some of the purchase-order lines (a partial delivery); the invoiced lines match, the total is lower.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx)
        {
            var remove = ctx.Rng.Next(1, ctx.Invoice.Lines.Count);   // keep at least one line
            var gone = ctx.Invoice.Lines.OrderBy(_ => ctx.Rng.Next()).Take(remove).ToList();
            foreach (var g in gone)
            {
                ctx.Invoice.Lines.Remove(g);
                ctx.Add("line", "", Status.Yellow, "", $"{g.Description} × {g.Quantity}", "purchase-order line not invoiced (partial delivery)", null, g.N);
            }
            Pick.TotalFinding(ctx, Status.Yellow, "the invoice total is below the purchase order: not every line was delivered");
        }
    }

    /// <summary>A purchase-order number that exists nowhere.</summary>
    public sealed class PoNumberUnknown : IInjector
    {
        public string Id => "PO_NUMBER_UNKNOWN";
        public string Expected => Status.Red;
        public string Description => "The invoice references a purchase-order number that does not exist in the purchase-order database; there is nothing to compare against.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx)
        {
            var was = ctx.Invoice.PoNumber;
            string other;
            do other = $"PO-{ctx.Rng.Next(10000000, 99999999)}"; while (other == was);
            ctx.Invoice.PoNumber = other;
            ctx.Add("po", "poNumber", Status.Red, other, was, "no purchase order with this number");
        }
    }

    /// <summary>Another currency, same numbers.</summary>
    public sealed class CurrencyMismatch : IInjector
    {
        public string Id => "CURRENCY_MISMATCH";
        public string Expected => Status.Red;
        public string Description => "The invoice is issued in a different currency than the purchase order; every number is unchanged, so the amounts are not what they look like.";
        public bool InExerciseSet => false;

        public void Inject(InjectionContext ctx)
        {
            var other = Pick.OtherCompany(ctx, c => c.Currency != ctx.Po.Currency);
            var was = ctx.Invoice.Currency;
            ctx.Invoice.Currency = other.Currency;
            ctx.Add("currency", "currency", Status.Red, other.Currency, was, "the invoice currency differs from the purchase order's");
        }
    }
}
