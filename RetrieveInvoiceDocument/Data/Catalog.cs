using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InvoiceGenerator.Data
{
    public class Company
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Country { get; set; }
        public string ContactName { get; set; }
        public string ContactPhone { get; set; }
        public string Currency { get; set; }
        public string TaxCode { get; set; }
        public string TaxName { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class Product
    {
        public string Name { get; set; }
        /// <summary>Base quantity: items sold in 1s, 5s or 10s. Order quantities are multiples of it.</summary>
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        /// <summary>Adjectives a supplier might put in front of the name ("Smooth", "Retractable").</summary>
        public List<string> Variations { get; set; } = new List<string>();
        /// <summary>Another name for the same thing ("Notes" for Sticky Notes).</summary>
        public string Alternative { get; set; }
    }

    /// <summary>The two CSVs under <c>Data/</c>: 37 companies in six countries, 50 products in three categories.</summary>
    public class Catalog
    {
        public List<Company> Companies { get; }
        public List<Product> Products { get; }

        public Catalog(string dataFolder)
        {
            Companies = Csv(Path.Combine(dataFolder, "companies.csv")).Where(v => v.Count >= 9).Select(v => new Company
            {
                Name = v[0], Address = v[1], Country = v[2], ContactName = v[3], ContactPhone = v[4], Currency = v[5],
                TaxCode = v[6], TaxName = v[7], TaxRate = decimal.Parse(v[8], CultureInfo.InvariantCulture)
            }).ToList();

            Products = Csv(Path.Combine(dataFolder, "products.csv")).Where(v => v.Count >= 6).Select(v => new Product
            {
                Name = v[0], Qty = int.Parse(v[1], CultureInfo.InvariantCulture), Price = decimal.Parse(v[2], CultureInfo.InvariantCulture),
                Category = v[3], Variations = v[4].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(), Alternative = v[5]
            }).ToList();

            if (Companies.Count < 4 || Products.Count < 10)
                throw new InvalidDataException($"Data folder {dataFolder} holds {Companies.Count} companies and {Products.Count} products; expected more.");
        }

        /// <summary>Rows of a CSV with quoted fields, header skipped, cells trimmed.</summary>
        private static IEnumerable<List<string>> Csv(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Sample data missing", path);
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = new List<string>(); var cell = new StringBuilder(); var quoted = false;
                foreach (var c in line)
                {
                    if (c == '"') quoted = !quoted;
                    else if (c == ',' && !quoted) { cells.Add(cell.ToString().Trim()); cell.Clear(); }
                    else cell.Append(c);
                }
                cells.Add(cell.ToString().Trim());
                yield return cells;
            }
        }
    }
}
