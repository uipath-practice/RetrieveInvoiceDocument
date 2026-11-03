using System;
using System.Collections.Generic;
using System.Linq;
using InvoiceGenerator.Data;

namespace InvoiceGenerator.Core
{
    /// <summary>
    /// Everything a run draws at random, computed once from the seed so the pair is reproducible: who
    /// sells to whom, which products, the numbers and the dates. Dates follow today on purpose — a
    /// purchase order from last month that is always "last month" reads right every time the lesson runs.
    /// Independent streams (<see cref="Stream"/>) keep an injector's own draws from shifting these.
    /// </summary>
    public sealed class Identity
    {
        public Identity(Catalog catalog, int seed, int linesQuantity)
        {
            Seed = seed;
            var rng = new Random(seed);
            LinesQuantity = Math.Clamp(linesQuantity, 2, 8);

            Seller = catalog.Companies[rng.Next(catalog.Companies.Count)];
            do Buyer = catalog.Companies[rng.Next(catalog.Companies.Count)]; while (ReferenceEquals(Buyer, Seller));

            // Eight grouped digits, like the v1 numbers the handbook screenshots show.
            InvoiceNumber = $"INV-{rng.Next(10000000, 99999999)}";
            PoNumber = $"PO-{rng.Next(10000000, 99999999)}";

            var today = DateTime.Today;
            PoDate = today.AddDays(-rng.Next(8, 30));
            InvoiceDate = today.AddDays(-rng.Next(0, 5));
            DueDate = InvoiceDate.AddDays(30);
            Today = today;

            // Products: distinct, and the set always contains one sold in multiples (quantity >= 5, so a
            // line can be split) and one priced at 5 or more (so a small price drift survives rounding).
            var pool = catalog.Products.ToList();
            var chosen = new List<Product>();
            Pick(chosen, pool, p => p.Qty >= 5, rng);
            Pick(chosen, pool, p => p.Price >= 5m, rng);
            while (chosen.Count < LinesQuantity && pool.Count > 0) Pick(chosen, pool, _ => true, rng);
            // Shuffle so the guaranteed ones are not always lines 1 and 2.
            Products = chosen.OrderBy(_ => rng.Next()).ToList();
            Quantities = Products.Select(p => p.Qty * rng.Next(1, 4)).ToList();
            DeliveryOffsets = Products.Select(_ => rng.Next(-14, 15)).ToList();
            Materials = Products.Select(_ => rng.Next(10000000, 99999999).ToString()).ToList();
        }

        private static void Pick(List<Product> chosen, List<Product> pool, Func<Product, bool> where, Random rng)
        {
            var candidates = pool.Where(where).ToList();
            if (candidates.Count == 0) return;
            var p = candidates[rng.Next(candidates.Count)];
            chosen.Add(p); pool.Remove(p);
            // The same product under two names (two conference tables) would confuse a reviewer; drop namesakes.
            pool.RemoveAll(x => x.Name == p.Name);
        }

        public int Seed { get; }
        public int LinesQuantity { get; }
        public Company Seller { get; }
        public Company Buyer { get; }
        public string InvoiceNumber { get; }
        public string PoNumber { get; }
        public DateTime Today { get; }
        public DateTime PoDate { get; }
        public DateTime InvoiceDate { get; }
        public DateTime DueDate { get; }
        public List<Product> Products { get; }
        public List<int> Quantities { get; }
        public List<int> DeliveryOffsets { get; }
        public List<string> Materials { get; }

        /// <summary>An independent random stream per consumer, so one consumer's extra draw cannot move another's.</summary>
        public Random Stream(string name) => new Random(Seed ^ StableHash(name));

        private static int StableHash(string text)
        {
            unchecked { var h = 17; foreach (var c in text) h = h * 31 + c; return h; }
        }
    }
}
