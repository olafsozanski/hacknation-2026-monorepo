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
    public const string Docx = "docx";
    public const string Txt = "txt";
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string TxtContentType = "text/plain; charset=utf-8";
    public const string UnsupportedFormatMessage = "Nieobsługiwany format pliku. Dozwolone wartości: docx, txt.";

    public const string AnonymizationNotice =
        "Dane oznaczone jako [dane zanonimizowane] wyłączono z udostępnienia ze względu na ochronę prywatności " +
        "osób fizycznych (art. 5 ust. 2 ustawy o dostępie do informacji publicznej).";

    private const string Font = "Arial";
    private const string FontSizeHalfPoints = "22";
    private const string FooterFontSizeHalfPoints = "16";
    private const string Language = "pl-PL";
    private const uint A4WidthTwips = 11906;
    private const uint A4HeightTwips = 16838;
    private const int MarginTwips = 1417;
    private const string ClosingIndentTwips = "5103";
    private const int MaxFileNameLength = 100;

    public static bool TryParseFormat(string? value, out string format)
    {
        format = string.IsNullOrWhiteSpace(value) ? Docx : value.Trim().ToLowerInvariant();
        return format is Docx or Txt;
    }

    public static string ContentType(string format) => format == Docx ? DocxContentType : TxtContentType;

    public static string PrepareText(string content, IEnumerable<Redaction> redactions)
    {
        var (text, replaced) = RedactionSet.ApplyApproved(content, redactions);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
        if (replaced == 0)
        {
            return text;
        }

        var closing = ClosingFormula().Matches(text).LastOrDefault();
        return closing is null
            ? $"{text}\n\n{AnonymizationNotice}"
            : $"{text[..closing.Index].TrimEnd()}\n\n{AnonymizationNotice}\n\n{text[closing.Index..]}";
    }

    public static string FileName(string caseNumber, string extension)
    {
        var safe = new string(caseNumber.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"{(safe.Length == 0 ? "projekt" : safe)}-odpowiedz.{extension}";
    }

    public static string AnonymizedFileName(string? originalName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(Path.GetFileName((originalName ?? "").Replace('\\', '/')));
        var safe = UnsafeFileNameChars().Replace(name, "-").Trim('-', '.');
        if (safe.Length > MaxFileNameLength)
        {
            safe = safe[..MaxFileNameLength].TrimEnd('-', '.');
        }

        return $"{(safe.Length == 0 ? "dokument" : safe)}-zanonimizowany.{extension}";
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
                body.Append(ToParagraph(paragraphs[i], StyleOf(paragraphs[i], i, paragraphs.Count)));
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

    private static ParagraphStyle StyleOf(string paragraph, int index, int count) => paragraph switch
    {
        _ when index == 0 && LetterHeader().IsMatch(paragraph) => ParagraphStyle.Header,
        _ when ClosingFormula().IsMatch(paragraph) => ParagraphStyle.Closing,
        _ when index == count - 1 && index > 0 && LetterFooter().IsMatch(paragraph) => ParagraphStyle.Footer,
        _ => ParagraphStyle.Body,
    };

    private static Styles DefaultStyles() => new(
        new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(
                new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                new FontSize { Val = FontSizeHalfPoints },
                new FontSizeComplexScript { Val = FontSizeHalfPoints },
                new Languages { Val = Language, EastAsia = Language, Bidi = Language })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }))));

    private static Paragraph ToParagraph(string text, ParagraphStyle style)
    {
        var run = new Run();
        if (style == ParagraphStyle.Footer)
        {
            run.Append(new RunProperties(
                new FontSize { Val = FooterFontSizeHalfPoints }, new FontSizeComplexScript { Val = FooterFontSizeHalfPoints }));
        }

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
        var properties = style switch
        {
            ParagraphStyle.Header => new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            ParagraphStyle.Closing => new ParagraphProperties(new Indentation { Left = ClosingIndentTwips }),
            _ => null,
        };
        if (properties is not null)
        {
            paragraph.Append(properties);
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

    private enum ParagraphStyle
    {
        Body,
        Header,
        Closing,
        Footer,
    }

    [GeneratedRegex(@"\n[ \t]*\n\s*")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"^(?:\p{Lu}[\p{Lu} .-]+)\n")]
    private static partial Regex LetterHeader();

    [GeneratedRegex(@"^[ \t]*(?:Łączę wyrazy szacunku|Z wyrazami szacunku|Z poważaniem)\b", RegexOptions.Multiline)]
    private static partial Regex ClosingFormula();

    [GeneratedRegex(@"\btel\.?.*\n?.*e-?mail", RegexOptions.IgnoreCase)]
    private static partial Regex LetterFooter();

    [GeneratedRegex(@"[^\p{L}\d_]+")]
    private static partial Regex UnsafeFileNameChars();
}
