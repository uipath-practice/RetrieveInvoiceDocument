using System;
using System.Globalization;
using System.Net;

namespace InvoiceGenerator.Core
{
    /// <summary>Numbers and dates as the documents print them. The JSON keeps raw values; only the renderings use this.</summary>
    public static class Formatting
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>"1,234.50" — one convention for every country; the extraction normalises it anyway.</summary>
        public static string Amount(decimal value) => value.ToString("N2", Inv);

        /// <summary>"1,234.50 USD".</summary>
        public static string Money(decimal value, string currency) => $"{Amount(value)} {currency}";

        public static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", Inv);

        /// <summary>SAP prints dd.MM.yyyy.</summary>
        public static string Sap(DateTime date) => date.ToString("dd.MM.yyyy", Inv);
        public static string Sap(string iso) => DateTime.TryParseExact(iso, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) ? Sap(d) : iso;

        /// <summary>"May 22, 2026" for the invoice.</summary>
        public static string Long(string iso) => DateTime.TryParseExact(iso, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d) ? d.ToString("MMMM dd, yyyy", Inv) : iso;

        public static string Percent(decimal rate) => rate.ToString("0.##", Inv) + "%";

        public static string Html(string text) => WebUtility.HtmlEncode(text ?? "");
    }
}
