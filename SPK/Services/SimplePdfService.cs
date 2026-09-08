using SistemPengurusanKehadiran.Models;
using System.Globalization;
using System.Text;

namespace SistemPengurusanKehadiran.Services;

public static class SimplePdfService
{
    public static byte[] WarningLetter(WarningLetter letter, Student student, string schoolName)
    {
        schoolName = string.IsNullOrWhiteSpace(schoolName) ? "SEKOLAH KEBANGSAAN SUNGAI PERGAM" : schoolName.Trim().ToUpperInvariant();
        var content = new StringBuilder();
        content.AppendLine("0.08 0.20 0.36 rg");
        Text(content, 52, 790, schoolName, 15, true);
        Text(content, 52, 770, "Sistem Pengurusan Kehadiran", 10, true);
        Text(content, 52, 755, "Dokumen pengurusan kehadiran murid", 9, false);
        content.AppendLine("0.35 0.45 0.60 RG 0.8 w 52 740 m 543 740 l S");
        content.AppendLine("0.12 0.12 0.12 rg");

        var ms = new CultureInfo("ms-MY");
        var y = 714d;
        if (!string.IsNullOrWhiteSpace(letter.ReferenceNo)) Text(content, 52, y, $"Rujukan: {letter.ReferenceNo}", 10, false);
        Text(content, 390, y, $"Tarikh: {letter.IssueDate.ToString("dd MMMM yyyy", ms)}", 10, false); y -= 34;

        Text(content, 52, y, string.IsNullOrWhiteSpace(letter.GuardianName) ? "Ibu Bapa / Penjaga" : letter.GuardianName, 10, true); y -= 16;
        foreach (var line in Wrap(letter.GuardianAddress, 72)) { Text(content, 52, y, line, 10, false); y -= 14; }
        y -= 10; Text(content, 52, y, "Tuan/Puan,", 10, false); y -= 30;

        content.AppendLine("0.08 0.20 0.36 rg");
        foreach (var line in Wrap(letter.Title.ToUpperInvariant(), 72)) { Text(content, 52, y, line, 12, true); y -= 16; }
        content.AppendLine("0.12 0.12 0.12 rg"); y -= 10;

        Text(content, 52, y, $"Nama Murid: {student.Nama}", 10, true); y -= 17;
        Text(content, 52, y, $"Kelas: {student.Kelas}    No. Murid / No. KP: {student.NoMurid}", 10, false); y -= 28;

        foreach (var p in Paragraphs(letter.Body))
        {
            foreach (var line in Wrap(p, 91)) { Text(content, 52, y, line, 10, false); y -= 15; }
            y -= 8;
        }

        content.AppendLine("0.08 0.20 0.36 rg");
        Text(content, 52, y, $"Jumlah rekod ketidakhadiran sehingga tarikh surat: {letter.TotalAbsences} hari", 10, true); y -= 30;
        content.AppendLine("0.12 0.12 0.12 rg");

        if (!string.IsNullOrWhiteSpace(letter.Notes))
        {
            Text(content, 52, y, "Catatan:", 10, true); y -= 16;
            foreach (var line in Wrap(letter.Notes, 91)) { Text(content, 52, y, line, 9, false); y -= 14; }
            y -= 10;
        }
        if (y < 100) y = 100;
        Text(content, 52, y, "Sekian, terima kasih.", 10, false); y -= 35;
        Text(content, 52, y, "______________________________", 10, false); y -= 16;
        Text(content, 52, y, "Guru / Pentadbir", 10, true); y -= 15;
        Text(content, 52, y, schoolName, 9, false);
        Text(content, 190, 35, "Dijana oleh Sistem Pengurusan Kehadiran", 8, false);

        return BuildPdf(content.ToString());
    }

    public static byte[] AnalyticsReport(AnalyticsReportData data, string schoolName, string periodText)
    {
        schoolName = string.IsNullOrWhiteSpace(schoolName) ? "SEKOLAH" : schoolName.Trim().ToUpperInvariant();
        var content = new StringBuilder();
        content.AppendLine("0.08 0.20 0.36 rg");
        Text(content, 48, 800, schoolName, 14, true);
        Text(content, 48, 780, "LAPORAN & ANALITIK KEHADIRAN", 16, true);
        Text(content, 48, 761, $"Tempoh: {periodText}    Kelas: {data.ClassFilter}", 9, false);
        content.AppendLine("0.35 0.45 0.60 RG 0.8 w 48 748 m 548 748 l S");
        content.AppendLine("0.12 0.12 0.12 rg");

        var y = 720d;
        Text(content, 48, y, $"Kadar Hadir: {data.AttendanceRate:0.0}%", 11, true);
        Text(content, 205, y, $"Tidak Hadir: {data.NotPresentRecords}", 10, true);
        Text(content, 335, y, $"Ponteng: {data.PontengRecords}", 10, true);
        Text(content, 438, y, $"Kes Aktif: {data.ActiveCases}", 10, true);
        y -= 20;
        Text(content, 48, y, $"Dipulihkan: {data.RecoveredCases}", 10, true);
        Text(content, 205, y, $"Jumlah Rekod: {data.TotalRecords}", 10, false);
        y -= 34;

        content.AppendLine("0.08 0.20 0.36 rg");
        Text(content, 48, y, "TREND KEHADIRAN", 11, true); y -= 18;
        content.AppendLine("0.12 0.12 0.12 rg");
        if (data.Trend.Count == 0)
        {
            Text(content, 48, y, "Tiada rekod kehadiran untuk tempoh ini.", 9, false); y -= 16;
        }
        else
        {
            foreach (var row in data.Trend.Take(12))
            {
                Text(content, 58, y, row.MonthLabel, 9, true);
                Text(content, 125, y, $"{row.AttendanceRate:0.0}%", 9, false);
                Text(content, 195, y, $"({row.Present}/{row.Total} hadir + lewat)", 9, false);
                y -= 15;
            }
        }
        y -= 12;

        content.AppendLine("0.08 0.20 0.36 rg");
        Text(content, 48, y, "PRESTASI MENGIKUT KELAS", 11, true); y -= 18;
        content.AppendLine("0.12 0.12 0.12 rg");
        Text(content, 48, y, "Kelas", 8, true);
        Text(content, 285, y, "Tidak", 8, true);
        Text(content, 350, y, "Ponteng", 8, true);
        Text(content, 430, y, "% Hadir", 8, true); y -= 14;
        foreach (var row in data.Classes.Take(14))
        {
            Text(content, 48, y, row.Kelas, 8, false);
            Text(content, 300, y, row.TidakHadir.ToString(CultureInfo.InvariantCulture), 8, false);
            Text(content, 370, y, row.Ponteng.ToString(CultureInfo.InvariantCulture), 8, false);
            Text(content, 430, y, row.AttendanceRateText, 8, false);
            y -= 13;
            if (y < 220) break;
        }
        y -= 12;

        if (y > 180)
        {
            content.AppendLine("0.08 0.20 0.36 rg");
            Text(content, 48, y, "MURID BERISIKO (TERATAS)", 11, true); y -= 18;
            content.AppendLine("0.12 0.12 0.12 rg");
            Text(content, 48, y, "Murid / Kelas", 8, true);
            Text(content, 305, y, "TH", 8, true);
            Text(content, 340, y, "P", 8, true);
            Text(content, 372, y, "L", 8, true);
            Text(content, 405, y, "%", 8, true);
            Text(content, 460, y, "Risiko", 8, true); y -= 14;
            foreach (var row in data.RiskStudents.Take(10))
            {
                var name = row.Nama.Length > 35 ? row.Nama[..35] : row.Nama;
                Text(content, 48, y, $"{name} / {row.Kelas}", 7, false);
                Text(content, 307, y, row.TidakHadir.ToString(CultureInfo.InvariantCulture), 7, false);
                Text(content, 343, y, row.Ponteng.ToString(CultureInfo.InvariantCulture), 7, false);
                Text(content, 375, y, row.Lewat.ToString(CultureInfo.InvariantCulture), 7, false);
                Text(content, 405, y, row.AttendanceRateText, 7, false);
                Text(content, 460, y, row.RiskText, 7, true);
                y -= 12;
                if (y < 65) break;
            }
            if (data.RiskStudents.Count == 0) Text(content, 48, y, "Tiada murid berisiko untuk tempoh ini.", 8, false);
        }

        Text(content, 180, 30, "Dijana oleh Sistem Pengurusan Kehadiran", 8, false);
        return BuildPdf(content.ToString());
    }

    public static byte[] GenericReport(ReportDocument report)
    {
        var pages = new List<string>();
        var content = new StringBuilder();
        double y = 0;
        int rowNo = 0;

        void NewPage()
        {
            if (content.Length > 0) pages.Add(content.ToString());
            content = new StringBuilder();
            content.AppendLine("0.08 0.20 0.36 rg");
            Text(content, 42, 806, string.IsNullOrWhiteSpace(report.SchoolName) ? "SEKOLAH" : report.SchoolName.ToUpperInvariant(), 12, true);
            Text(content, 42, 786, report.Title, 14, true);
            Text(content, 42, 768, $"Tempoh: {report.PeriodText}    Kelas: {report.ClassText}", 8, false);
            content.AppendLine("0.35 0.45 0.60 RG 0.8 w 42 755 m 553 755 l S");
            content.AppendLine("0.12 0.12 0.12 rg");
            y = 735;
        }

        NewPage();
        foreach (var sum in report.Summary)
        {
            foreach (var line in Wrap("• " + sum, 105)) { Text(content, 46, y, line, 8, false); y -= 12; }
        }
        y -= 8;

        var headerLine = string.Join(" | ", report.Headers);
        foreach (var line in Wrap(headerLine, 105)) { Text(content, 46, y, line, 8, true); y -= 12; }
        content.AppendLine($"0.80 0.84 0.90 RG 0.5 w 46 {y + 5:0.##} m 548 {y + 5:0.##} l S");
        y -= 5;

        foreach (var row in report.Rows)
        {
            var joined = $"{++rowNo}. " + string.Join(" | ", row.Select(x => (x ?? "").Replace("\r", " ").Replace("\n", " ")));
            var lines = Wrap(joined, 112).ToList();
            var needed = Math.Max(1, lines.Count) * 11 + 5;
            if (y - needed < 50)
            {
                Text(content, 245, 25, $"Halaman {pages.Count + 1}", 7, false);
                NewPage();
                foreach (var line in Wrap(headerLine, 105)) { Text(content, 46, y, line, 8, true); y -= 12; }
                content.AppendLine($"0.80 0.84 0.90 RG 0.5 w 46 {y + 5:0.##} m 548 {y + 5:0.##} l S");
                y -= 5;
            }
            foreach (var line in lines) { Text(content, 46, y, line, 7, false); y -= 11; }
            y -= 4;
        }

        if (report.Rows.Count == 0)
        {
            Text(content, 46, y, "Tiada rekod untuk penapis yang dipilih.", 9, false);
        }
        Text(content, 185, 25, "Dijana oleh Sistem Pengurusan Kehadiran", 7, false);
        pages.Add(content.ToString());
        return BuildPdfPages(pages);
    }

    private static byte[] BuildPdfPages(IReadOnlyList<string> pageStreams)
    {
        var enc = Encoding.ASCII;
        var pageCount = Math.Max(1, pageStreams.Count);
        var objects = new List<byte[]>();
        // 1 Catalog, 2 Pages. Page/content pairs start at 3. Fonts are last two objects.
        objects.Add(enc.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        var font1Id = 3 + pageCount * 2;
        var font2Id = font1Id + 1;
        var kids = new StringBuilder("[");
        for (int i = 0; i < pageCount; i++) kids.Append(3 + i * 2).Append(" 0 R ");
        kids.Append(']');
        objects.Add(enc.GetBytes($"<< /Type /Pages /Kids {kids} /Count {pageCount} >>"));

        for (int i = 0; i < pageCount; i++)
        {
            var pageId = 3 + i * 2;
            var contentId = pageId + 1;
            objects.Add(enc.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font << /F1 {font1Id} 0 R /F2 {font2Id} 0 R >> >> /Contents {contentId} 0 R >>"));
            var streamBytes = enc.GetBytes(pageStreams[i]);
            objects.Add(Combine(enc.GetBytes($"<< /Length {streamBytes.Length} >>\nstream\n"), streamBytes, enc.GetBytes("\nendstream")));
        }
        objects.Add(enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));

        using var ms = new MemoryStream();
        void W(string x) { var b = enc.GetBytes(x); ms.Write(b, 0, b.Length); }
        W("%PDF-1.4\n%SPK\n");
        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position); W($"{i + 1} 0 obj\n"); ms.Write(objects[i], 0, objects[i].Length); W("\nendobj\n");
        }
        var xref = ms.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++) W(offsets[i].ToString("0000000000") + " 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static IEnumerable<string> Paragraphs(string text) => (text ?? "").Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0);

    private static IEnumerable<string> Wrap(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var para in text.Replace("\r", "").Split('\n'))
        {
            var words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = new StringBuilder();
            foreach (var word in words)
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > max) { yield return line.ToString(); line.Clear(); }
                if (line.Length > 0) line.Append(' '); line.Append(word);
            }
            if (line.Length > 0) yield return line.ToString();
        }
    }

    private static void Text(StringBuilder sb, double x, double y, string text, int size, bool bold)
    {
        sb.Append("BT /").Append(bold ? "F2" : "F1").Append(' ').Append(size).Append(" Tf ")
          .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
          .Append(y.ToString("0.##", CultureInfo.InvariantCulture)).Append(" Td (")
          .Append(Escape(text)).AppendLine(") Tj ET");
    }

    private static string Escape(string text)
    {
        var ascii = new string((text ?? "").Select(ch => ch >= 32 && ch <= 126 ? ch : ch == '\t' ? ' ' : '?').ToArray());
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }

    private static byte[] BuildPdf(string stream)
    {
        var enc = Encoding.ASCII;
        var streamBytes = enc.GetBytes(stream);
        var objects = new List<byte[]>();
        objects.Add(enc.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(enc.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        objects.Add(enc.GetBytes("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>"));
        objects.Add(enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(enc.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));
        objects.Add(Combine(enc.GetBytes($"<< /Length {streamBytes.Length} >>\nstream\n"), streamBytes, enc.GetBytes("\nendstream")));

        using var ms = new MemoryStream();
        void W(string s) { var b = enc.GetBytes(s); ms.Write(b, 0, b.Length); }
        W("%PDF-1.4\n%SPK\n");
        var offsets = new List<long> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(ms.Position); W($"{i + 1} 0 obj\n"); ms.Write(objects[i], 0, objects[i].Length); W("\nendobj\n");
        }
        var xref = ms.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++) W(offsets[i].ToString("0000000000") + " 00000 n \n");
        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    private static byte[] Combine(params byte[][] arrays)
    {
        var len = arrays.Sum(x => x.Length); var result = new byte[len]; var p = 0;
        foreach (var a in arrays) { Buffer.BlockCopy(a, 0, result, p, a.Length); p += a.Length; }
        return result;
    }
}
