using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EduGuardProject.Services;

public sealed record ReportExportFile(byte[] Content, string ContentType, string FileName);

public sealed class ReportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ReportExportFile Export(string reportType, string format, object report)
    {
        if (string.IsNullOrWhiteSpace(reportType))
            throw new ArgumentException("reportType is required.");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report, JsonOptions));
        var normalizedFormat = format?.Trim().ToLowerInvariant();
        var safeType = reportType.Trim().ToLowerInvariant();
        var fileName = $"eduguard-{safeType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        return normalizedFormat switch
        {
            "xlsx" or "excel" => new(BuildExcel(safeType, json.RootElement),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx"),
            "pdf" => new(BuildPdf(safeType, json.RootElement), "application/pdf", $"{fileName}.pdf"),
            _ => throw new ArgumentException("format must be pdf or xlsx.")
        };
    }

    private static byte[] BuildExcel(string reportType, JsonElement root)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        sheet.ShowGridLines = false;

        sheet.Cell("A1").Value = $"EDUGUARD {reportType.ToUpperInvariant()} REPORT";
        sheet.Range("A1:H1").Merge().Style
            .Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#0F766E"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        sheet.Row(1).Height = 30;
        sheet.Cell("A2").Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

        var row = 4;
        sheet.Cell(row, 1).Value = "Summary";
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#0F766E"));
        row++;
        if (root.TryGetProperty("summary", out var summary))
        {
            foreach (var property in summary.EnumerateObject())
            {
                sheet.Cell(row, 1).Value = Humanize(property.Name);
                sheet.Cell(row, 2).Value = Display(property.Value);
                row++;
            }
        }

        row++;
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            var rows = items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
            var columns = rows.SelectMany(x => x.EnumerateObject().Select(p => p.Name)).Distinct().ToList();
            if (columns.Count > 0)
            {
                var headerRow = row;
                for (var column = 0; column < columns.Count; column++)
                    sheet.Cell(row, column + 1).Value = Humanize(columns[column]);

                sheet.Range(row, 1, row, columns.Count).Style
                    .Font.SetBold().Font.SetFontColor(XLColor.White)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#334155"));

                foreach (var item in rows)
                {
                    row++;
                    for (var column = 0; column < columns.Count; column++)
                        if (item.TryGetProperty(columns[column], out var value))
                            SetCellValue(sheet.Cell(row, column + 1), value);
                }

                var table = sheet.Range(headerRow, 1, row, columns.Count).CreateTable("ReportItems");
                table.Theme = XLTableTheme.TableStyleMedium2;
                sheet.SheetView.FreezeRows(headerRow);
            }
        }

        sheet.ColumnsUsed().AdjustToContents(8, 45);
        sheet.RowsUsed().Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(string reportType, JsonElement root)
    {
        EnsureFontResolver();
        using var document = new PdfDocument();
        document.Info.Title = $"EduGuard {reportType} report";

        var items = root.TryGetProperty("items", out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList()
            : [];
        PdfPage? page = null;
        XGraphics? graphics = null;
        var titleFont = new XFont("ReportFont", 18, XFontStyleEx.Bold);
        var headerFont = new XFont("ReportFont", 10, XFontStyleEx.Bold);
        var bodyFont = new XFont("ReportFont", 9);
        const double margin = 36;
        double y = 0;

        void NewPage()
        {
            graphics?.Dispose();
            page = document.AddPage();
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            graphics = XGraphics.FromPdfPage(page);
            y = margin;
            graphics.DrawString($"EduGuard {Humanize(reportType)} Report", titleFont, XBrushes.DarkSlateGray,
                new XRect(margin, y, page.Width.Point - margin * 2, 24), XStringFormats.TopLeft);
            y += 30;
            graphics.DrawString($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC", bodyFont, XBrushes.Gray,
                new XPoint(margin, y));
            y += 18;
        }

        NewPage();
        if (root.TryGetProperty("summary", out var summary))
        {
            foreach (var property in summary.EnumerateObject())
            {
                graphics!.DrawString($"{Humanize(property.Name)}: {Display(property.Value)}", bodyFont, XBrushes.Black,
                    new XPoint(margin, y));
                y += 12;
            }
            y += 10;
        }

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var properties = items[itemIndex].EnumerateObject().ToList();
            var requiredHeight = 24 + properties.Sum(p => Math.Max(1, (Display(p.Value).Length + 89) / 90) * 13);
            if (y + requiredHeight > page!.Height.Point - margin)
                NewPage();

            graphics!.DrawString($"Record {itemIndex + 1}", headerFont, XBrushes.DarkSlateGray, new XPoint(margin, y));
            y += 16;
            foreach (var property in properties)
            {
                var text = $"{Humanize(property.Name)}: {Display(property.Value)}";
                foreach (var line in Wrap(text, 90))
                {
                    if (y + 13 > page.Height.Point - margin)
                        NewPage();
                    graphics.DrawString(line, bodyFont, XBrushes.Black, new XPoint(margin + 8, y));
                    y += 13;
                }
            }
            graphics.DrawLine(XPens.LightGray, margin, y, page.Width.Point - margin, y);
            y += 12;
        }

        graphics?.Dispose();
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void EnsureFontResolver()
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new ReportFontResolver();
    }

    private static void SetCellValue(IXLCell cell, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDecimal(out var number):
                cell.Value = number;
                cell.Style.NumberFormat.Format = "#,##0.##";
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                cell.Value = value.GetBoolean();
                break;
            case JsonValueKind.String when value.TryGetDateTime(out var date):
                cell.Value = date;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            default:
                cell.Value = Display(value);
                break;
        }
    }

    private static string Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Null => "",
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(Display)),
        JsonValueKind.Object => string.Join(", ", value.EnumerateObject().Select(p => $"{Humanize(p.Name)}: {Display(p.Value)}")),
        _ => value.GetRawText()
    };

    private static string Humanize(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()))
            .Replace("_", " ").Trim();

    private static IEnumerable<string> Wrap(string value, int width)
    {
        for (var index = 0; index < value.Length; index += width)
            yield return value.Substring(index, Math.Min(width, value.Length - index));
        if (value.Length == 0)
            yield return "";
    }
}

internal sealed class ReportFontResolver : IFontResolver
{
    private static readonly string[] RegularCandidates =
    [
        @"C:\Windows\Fonts\arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    ];

    private static readonly string[] BoldCandidates =
    [
        @"C:\Windows\Fonts\arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    ];

    public byte[] GetFont(string faceName) => File.ReadAllBytes(ResolvePath(faceName == "ReportFontBold" ? BoldCandidates : RegularCandidates));

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "ReportFontBold" : "ReportFont");

    private static string ResolvePath(IEnumerable<string> candidates) =>
        candidates.FirstOrDefault(File.Exists)
        ?? throw new InvalidOperationException("No report font is installed. Install Arial or DejaVu Sans.");
}
