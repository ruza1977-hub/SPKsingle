using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SistemPengurusanKehadiran.Models;

namespace SistemPengurusanKehadiran.Services;

public static class WordReportService
{
    public static byte[] Build(ReportDocument report)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            body.Append(Paragraph(report.SchoolName.ToUpperInvariant(), 28, true, "17365D"));
            body.Append(Paragraph(report.Title, 32, true, "17365D"));
            body.Append(Paragraph($"Tempoh: {report.PeriodText}    Kelas: {report.ClassText}", 20, false, "555555"));
            body.Append(new Paragraph(new Run(new Text(""))));

            foreach (var line in report.Summary)
                body.Append(Paragraph("• " + line, 20, false, "333333"));

            body.Append(new Paragraph(new Run(new Text(""))));
            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = "B8C4D6" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "B8C4D6" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "B8C4D6" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = "B8C4D6" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 3, Color = "DDE4ED" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 3, Color = "DDE4ED" }))); 

            var header = new TableRow();
            foreach (var h in report.Headers) header.Append(Cell(h, true));
            table.Append(header);
            foreach (var row in report.Rows)
            {
                var tr = new TableRow();
                foreach (var cell in row) tr.Append(Cell(cell, false));
                table.Append(tr);
            }
            body.Append(table);
            body.Append(new Paragraph(new Run(new Break())));
            body.Append(Paragraph($"Dijana oleh Sistem Pengurusan Kehadiran pada {DateTime.Now:dd/MM/yyyy HH:mm}", 16, false, "777777"));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Paragraph Paragraph(string text, int halfPoints, bool bold, string color)
    {
        var rp = new RunProperties(new FontSize { Val = halfPoints.ToString() }, new Color { Val = color });
        if (bold) rp.Append(new Bold());
        return new Paragraph(new Run(rp, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static TableCell Cell(string? text, bool header)
    {
        var tc = new TableCell();
        if (header)
            tc.Append(new TableCellProperties(new Shading { Fill = "EAF1F8", Val = ShadingPatternValues.Clear }));
        var rp = new RunProperties(new FontSize { Val = "16" });
        if (header) rp.Append(new Bold());
        tc.Append(new Paragraph(new Run(rp, new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve })));
        return tc;
    }
}
