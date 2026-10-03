using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Project_HR_1.Services.Documents;

public class RosterPdfData
{
    public string TitleAr { get; set; } = "كشف الموظفين";
    public string ScopeSummary { get; set; } = "";
    public string FilterSummary { get; set; } = "";   // one line, empty = hidden
    public bool Portrait { get; set; }
    public bool RowNumbers { get; set; } = true;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public List<string> Headers { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();
}

public interface IRosterPdfBuilder
{
    byte[] Build(RosterPdfData data);
}

public class RosterPdfBuilder : IRosterPdfBuilder
{
    // Why this is sized dynamically: with many columns on a fixed A4 page every column became a few points
    // wide, and any word longer than that (an IBAN, an e-mail) threw QuestPDF's DocumentLayoutException
    // ("conflicting size constraints"). Now each column gets the width its content needs, the page grows to
    // fit all columns (the PDF viewer scales it down when printing), and very long tokens get soft break points.
    private const float FontSize = 7.5f;
    private const float CharWidth = FontSize * 0.55f;   // average glyph width estimate (Arabic + digits)
    private const string Navy = "#1a2744", Gold = "#c8a84b";   // same brand tokens as wwwroot/css/app.css
    private const float MinCol = 34f, MaxCol = 170f, CellPadding = 3f, Margin = 24f;

    public byte[] Build(RosterPdfData d)
    {
        var headers = d.Headers.Select(Soft).ToList();
        var rows = d.Rows.Select(r => r.Select(c => Soft(c ?? "")).ToArray()).ToList();

        var widths = new List<float>();
        if (d.RowNumbers) widths.Add(26f);   // "#" column
        for (int i = 0; i < headers.Count; i++)
        {
            var longest = Math.Max(Math.Min(headers[i].Length, 16), rows.Count == 0 ? 0 : rows.Max(r => i < r.Length ? LongestLine(r[i]) : 0));
            widths.Add(Math.Clamp(longest * CharWidth + CellPadding * 2 + 4, MinCol, MaxCol));
        }

        var a4 = d.Portrait ? PageSizes.A4 : PageSizes.A4.Landscape();
        var pageWidth = Math.Max(a4.Width, widths.Sum() + Margin * 2 + 2);
        var pageHeight = Math.Max(a4.Height, pageWidth * a4.Height / a4.Width);   // keep the chosen proportions

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageWidth, pageHeight, Unit.Point);
                page.Margin(Margin);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontSize(FontSize));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(d.TitleAr).FontSize(16).Bold().FontColor(Navy);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"{d.ScopeSummary} — {d.Rows.Count} موظف").FontSize(9).FontColor(Colors.Grey.Darken1);
                        row.ConstantItem(120).AlignLeft().Text($"{d.GeneratedAt:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    if (!string.IsNullOrWhiteSpace(d.FilterSummary))
                        col.Item().PaddingTop(2).Text(d.FilterSummary).FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Navy);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var w in widths) columns.ConstantColumn(w);
                    });

                    table.Header(header =>
                    {
                        if (d.RowNumbers)
                            header.Cell().Background(Navy).BorderBottom(1.5f).BorderColor(Gold).Padding(CellPadding).Text("#").FontColor(Colors.White).Bold();
                        foreach (var h in headers)
                            header.Cell().Background(Navy).BorderBottom(1.5f).BorderColor(Gold).Padding(CellPadding).Text(h).FontColor(Colors.White).Bold();
                    });

                    for (int r = 0; r < rows.Count; r++)
                    {
                        var bg = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                        if (d.RowNumbers)
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(CellPadding).Text((r + 1).ToString());
                        for (int c = 0; c < headers.Count; c++)
                        {
                            var value = c < rows[r].Length ? rows[r][c] : "";
                            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(CellPadding).Text(value);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("صفحة ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // Characters the column should fit on one line: the whole value if short, else ~40 chars (it wraps).
    private static int LongestLine(string s) => Math.Min(s.Replace("\u200B", "").Length, 40);

    // Insert zero-width spaces into very long unbroken tokens (IBANs, e-mails) so they can wrap inside a column.
    private static string Soft(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return string.Join(' ', s.Split(' ').Select(w => w.Length <= 26 ? w
            : string.Join("\u200B", Enumerable.Range(0, (w.Length + 12) / 13).Select(i => w.Substring(i * 13, Math.Min(13, w.Length - i * 13))))));
    }
}
