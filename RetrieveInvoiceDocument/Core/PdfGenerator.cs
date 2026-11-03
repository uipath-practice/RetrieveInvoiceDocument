using System;
using System.IO;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using PageSize = iText.Kernel.Geom.PageSize;

namespace InvoiceGenerator.Core
{
    /// <summary>HTML to PDF with iText 7 pdfHTML — no browser, so it runs on the serverless robot. Same as the ClaimCase generator.</summary>
    public class PdfGenerator
    {
        public void Convert(string html, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(html)) throw new ArgumentException("Nothing to convert: the rendered template was empty.", nameof(html));
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            try
            {
                using (var writer = new PdfWriter(outputPath))
                using (var document = new PdfDocument(writer))
                {
                    document.SetDefaultPageSize(PageSize.A4);
                    var properties = new ConverterProperties();
                    properties.SetBaseUri(AppContext.BaseDirectory);
                    HtmlConverter.ConvertToPdf(html, document, properties);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR converting {Path.GetFileName(outputPath)}: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"       inner: {ex.InnerException.Message}");
                throw;
            }
        }
    }
}
