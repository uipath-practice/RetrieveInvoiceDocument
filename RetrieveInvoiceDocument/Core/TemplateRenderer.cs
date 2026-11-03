using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace InvoiceGenerator.Core
{
    /// <summary>
    /// Replaces <c>{{KEY}}</c> tokens with values. An unmatched token stays on the page so a missing
    /// field is loud, not invisible. Values are inserted as given: callers HTML-encode text and pass
    /// pre-rendered markup (table rows) as is.
    /// </summary>
    public class TemplateRenderer
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        public string Render(string template, Dictionary<string, string> fields) =>
            Placeholder.Replace(template, m => fields.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }
}
