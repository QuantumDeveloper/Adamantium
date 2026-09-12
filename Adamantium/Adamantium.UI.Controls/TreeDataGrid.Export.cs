using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Adamantium.UI.Controls;

/// <summary>Writing the table out, with no library behind either format: CSV is text, and an .xlsx is a zip of a few
/// XML parts, which the platform can already make. Calling a CSV file an Excel export is simply untrue, and it is the
/// user who finds that out.</summary>
public partial class TreeDataGrid
{
    /// <summary>Writes what the table is showing as CSV: a header line, then one line per record.
    /// <para><paramref name="separator"/> is a parameter because there is no right answer - a comma is what the format
    /// is named after, and a spreadsheet whose locale lists with semicolons puts a comma-separated file in one
    /// column.</para></summary>
    public void ExportCsv(TextWriter writer, char separator = ',')
    {
        if (writer == null) return;

        var columns = ExportColumns();
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0) writer.Write(separator);
            writer.Write(Escape(columns[i].Header?.ToString(), separator));
        }

        writer.Write("\r\n");

        foreach (var row in ExportRows())
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0) writer.Write(separator);
                writer.Write(Escape(Text(ValueOf(columns[i], row)), separator));
            }

            writer.Write("\r\n");
        }
    }

    /// <summary>Writes what the table is showing as a real .xlsx, so numbers arrive as numbers rather than as text a
    /// spreadsheet has to be talked into converting.</summary>
    public void ExportXlsx(Stream stream)
    {
        if (stream == null) return;

        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Write(zip, "[Content_Types].xml", ContentTypes);
        Write(zip, "_rels/.rels", PackageRelationships);
        Write(zip, "xl/workbook.xml", Workbook);
        Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
        WriteSheet(zip, ExportColumns(), ExportRows());
    }

    // IsVisible, not IsShown: a column the table is GROUPED BY still goes out. Its value is on every record, and on
    // screen it lives in the captions - a file has no captions, so leaving it out drops the very field the table is
    // organised by.
    private List<DataGridColumn> ExportColumns()
    {
        var columns = new List<DataGridColumn>();
        foreach (var column in Columns)
            if (column.IsVisible) columns.Add(column);

        return columns;
    }

    // Filtered and sorted as the table is, and the whole depth of the tree. What is OPEN is not part of it: a collapsed
    // branch is a fold of the view, not an absence of data.
    private List<object> ExportRows()
    {
        var rows = new List<object>();
        Collect(_shapedRoots ?? Group(Shape(_roots)), rows);
        return rows;
    }

    private void Collect(IEnumerable source, List<object> into)
    {
        if (source == null) return;

        foreach (var node in source)
        {
            if (node is DataGridGroup group)
            {
                Collect(group.Children, into);
                continue;
            }

            into.Add(node);
            Collect(ShapedChildrenOf(node), into);
        }
    }

    // Invariant: a file is written to be read elsewhere, and a number whose decimal separator depends on the machine
    // that wrote it is a number the next machine reads wrong.
    private static string Text(object value) => value switch
    {
        null => string.Empty,
        string text => text,
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static string Escape(string value, char separator)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOf(separator) < 0 && value.IndexOf('"') < 0
            && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0) return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string XmlHead = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private const string ContentTypes = XmlHead +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "</Types>";

    private const string PackageRelationships = XmlHead +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string Workbook = XmlHead +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        "<sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";

    private const string WorkbookRelationships = XmlHead +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "</Relationships>";

    // Straight into the zip entry. Built as one string, a real table is enormous - ten thousand rows of thirteen
    // columns came to a fifty-three megabyte sheet, and holding that whole thing in memory to hand it over in one piece
    // is a cost with nothing to show for it.
    private void WriteSheet(ZipArchive zip, List<DataGridColumn> columns, List<object> rows)
    {
        var letters = new string[columns.Count];
        for (var i = 0; i < letters.Length; i++) letters[i] = Letters(i);

        using var entry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open();
        using var xml = new StreamWriter(entry, new UTF8Encoding(false));

        xml.Write(XmlHead);
        xml.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

        xml.Write("<row r=\"1\">");
        for (var i = 0; i < columns.Count; i++) InlineString(xml, letters[i], 1, columns[i].Header?.ToString());
        xml.Write("</row>");

        for (var r = 0; r < rows.Count; r++)
        {
            xml.Write("<row r=\"");
            xml.Write(r + 2);
            xml.Write("\">");
            for (var i = 0; i < columns.Count; i++) Cell(xml, letters[i], r + 2, ValueOf(columns[i], rows[r]));
            xml.Write("</row>");
        }

        xml.Write("</sheetData></worksheet>");
    }

    // A number as a number and a flag as a flag - the whole reason for writing a real workbook rather than a CSV with a
    // different extension. A column of numbers that arrives as text cannot be summed without being converted first.
    private static void Cell(TextWriter xml, string letters, int row, object value)
    {
        switch (value)
        {
            case null:
                return;
            case bool flag:
                Open(xml, letters, row, "\" t=\"b\"><v>");
                xml.Write(flag ? 1 : 0);
                xml.Write("</v></c>");
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                Open(xml, letters, row, "\"><v>");
                xml.Write(Text(value));
                xml.Write("</v></c>");
                return;
            default:
                InlineString(xml, letters, row, Text(value));
                return;
        }
    }

    // Inline, so the workbook needs no shared-strings part: one part fewer to get right, and what that part saves is for
    // the same text over and over, which a table exported once is not.
    private static void InlineString(TextWriter xml, string letters, int row, string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        Open(xml, letters, row, "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
        xml.Write(Xml(text));
        xml.Write("</t></is></c>");
    }

    private static void Open(TextWriter xml, string letters, int row, string tail)
    {
        xml.Write("<c r=\"");
        xml.Write(letters);
        xml.Write(row);
        xml.Write(tail);
    }

    private static string Xml(string text) => text
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // A, B ... Z, AA: how a cell says which column it is in. Worked out once per column, not once per cell.
    private static string Letters(int column)
    {
        var letters = string.Empty;
        for (var at = column; ; at = at / 26 - 1)
        {
            letters = (char)('A' + at % 26) + letters;
            if (at < 26) break;
        }

        return letters;
    }
}
