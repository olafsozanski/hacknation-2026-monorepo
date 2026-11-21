using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Features.Anonymization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ClerkAssistant.Api.Features.Drafts;

public static partial class DraftExport
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string TxtContentType = "text/plain; charset=utf-8";

    public const string AnonymizationNotice =
        "Dane oznaczone jako [dane zanonimizowane] wyłączono z udostępnienia ze względu na ochronę prywatności " +
        "osób fizycznych (art. 5 ust. 2 ustawy o dostępie do informacji publicznej).";

    private const string Font = "Arial";
    private const string FontSizeHalfPoints = "22";
    private const string Language = "pl-PL";
    private const uint A4WidthTwips = 11906;
    private const uint A4HeightTwips = 16838;
    private const int MarginTwips = 1417;

    public static string PrepareText(string content, IEnumerable<Redaction> redactions)
    {
        var (text, replaced) = RedactionSet.ApplyApproved(content, redactions);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
        return replaced > 0 ? $"{text}\n\n{AnonymizationNotice}" : text;
    }

    public static string FileName(string caseNumber, string extension)
    {
        var safe = new string(caseNumber.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "projekt" : safe)}-odpowiedz.{extension}";
    }

    public static byte[] ToTxt(string text) => Encoding.UTF8.GetBytes(text);

    public static byte[] ToDocx(string text, string title)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = DefaultStyles();

            var body = new Body();
            var paragraphs = ParagraphBreak().Split(XmlSafe(text).Trim()).Where(p => p.Trim().Length > 0).ToList();
            for (var i = 0; i < paragraphs.Count; i++)
            {
                body.Append(ToParagraph(paragraphs[i], alignRight: i == 0 && PlaceAndDate().IsMatch(paragraphs[i])));
            }

            body.Append(new SectionProperties(
                new PageSize { Width = A4WidthTwips, Height = A4HeightTwips },
                new PageMargin
                {
                    Top = MarginTwips,
                    Bottom = MarginTwips,
                    Left = MarginTwips,
                    Right = MarginTwips,
                    Header = 708,
                    Footer = 708,
                    Gutter = 0,
                }));
            main.Document = new Document(body);
            document.PackageProperties.Title = XmlSafe(title);
            document.PackageProperties.Language = Language;
        }

        return stream.ToArray();
    }

    private static Styles DefaultStyles() => new(
        new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                new FontSize { Val = FontSizeHalfPoints },
                new FontSizeComplexScript { Val = FontSizeHalfPoints },
                new Languages { Val = Language, EastAsia = Language, Bidi = Language })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }))));

    private static Paragraph ToParagraph(string text, bool alignRight)
    {
        var run = new Run();
        var lines = text.Trim('\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                run.Append(new Break());
            }

            run.Append(new Text(lines[i].TrimEnd()) { Space = SpaceProcessingModeValues.Preserve });
        }

        var paragraph = new Paragraph();
        if (alignRight)
        {
            paragraph.Append(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
        }

        paragraph.Append(run);
        return paragraph;
    }

    private static string XmlSafe(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                builder.Append(text[i]).Append(text[++i]);
            }
            else if (XmlConvert.IsXmlChar(text[i]))
            {
                builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"^\p{L}[\p{L} .-]*, dnia ")]
    private static partial Regex PlaceAndDate();
}
