using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ClerkAssistant.Api.Domain;
using ClerkAssistant.Api.Infrastructure.Security;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ClerkAssistant.Api.Infrastructure.Documents;

public sealed record ReadDocument(string FileName, string ContentType, byte[] Content, string Text)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public StoredFile ToStoredFile(Guid userId, DateTimeOffset now) => new()
    {
        FileName = FileName,
        ContentType = ContentType,
        SizeBytes = Content.LongLength,
        Content = Content,
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Content)),
        ExtractedText = Text,
        CreatedAt = now,
        CreatedById = userId,
    };
}

public sealed class DocumentRejectedException(string message) : Exception(message);

public sealed class DocumentReader
{
    private const int MaxPdfPages = 500;
    private const int MaxZipEntries = 2000;
    private const long MaxUncompressedBytes = 200L * 1024 * 1024;
    private const int MaxTextLength = 500_000;

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".txt"] = "text/plain; charset=utf-8",
    };

    public static IReadOnlyCollection<string> SupportedExtensions => ContentTypes.Keys;

    public async Task<ReadDocument> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            throw new DocumentRejectedException("Plik jest pusty.");
        }

        if (file.Length > SecurityExtensions.MaxUploadBytes)
        {
            throw new DocumentRejectedException("Plik jest za duży. Maksymalny rozmiar to 20 MB.");
        }

        var fileName = SanitizeFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new DocumentRejectedException(extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
                ? "Format .doc nie jest obsługiwany. Zapisz dokument jako .docx lub PDF."
                : "Nieobsługiwany format pliku. Dozwolone są pliki PDF, DOCX i TXT.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var content = buffer.ToArray();

        var text = extension.ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(content),
            ".docx" => ExtractDocx(content),
            _ => ExtractPlainText(content),
        };

        return new ReadDocument(fileName, contentType, content, Normalize(text));
    }

    private static string ExtractPdf(byte[] content)
    {
        if (!content.AsSpan().StartsWith("%PDF-"u8))
        {
            throw new DocumentRejectedException("Plik nie jest prawidłowym dokumentem PDF.");
        }

        try
        {
            using var pdf = PdfDocument.Open(content);
            if (pdf.NumberOfPages > MaxPdfPages)
            {
                throw new DocumentRejectedException($"Dokument ma zbyt wiele stron (maksymalnie {MaxPdfPages}).");
            }

            var text = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
            }

            return text.ToString();
        }
        catch (DocumentRejectedException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new DocumentRejectedException("Nie udało się odczytać dokumentu PDF. Plik może być uszkodzony lub zaszyfrowany.");
        }
    }

    private static string ExtractDocx(byte[] content)
    {
        if (!content.AsSpan().StartsWith("PK\u0003\u0004"u8))
        {
            throw new DocumentRejectedException("Plik nie jest prawidłowym dokumentem DOCX.");
        }

        EnsureSafeArchive(content);
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new DocumentRejectedException("Dokument DOCX nie zawiera treści.");
            return string.Join('\n', body.Descendants<Paragraph>().Select(p => p.InnerText));
        }
        catch (DocumentRejectedException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new DocumentRejectedException("Nie udało się odczytać dokumentu DOCX. Plik może być uszkodzony.");
        }
    }

    private static void EnsureSafeArchive(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxZipEntries || zip.Entries.Sum(e => e.Length) > MaxUncompressedBytes)
            {
                throw new DocumentRejectedException("Dokument DOCX ma podejrzaną strukturę i został odrzucony.");
            }

            if (zip.GetEntry("word/document.xml") is null)
            {
                throw new DocumentRejectedException("Plik nie jest prawidłowym dokumentem DOCX.");
            }
        }
        catch (InvalidDataException)
        {
            throw new DocumentRejectedException("Plik nie jest prawidłowym dokumentem DOCX.");
        }
    }

    private static string ExtractPlainText(byte[] content)
    {
        if (content.Contains((byte)0))
        {
            throw new DocumentRejectedException("Plik tekstowy zawiera nieprawidłowe znaki.");
        }

        return new UTF8Encoding(false, false).GetString(content);
    }

    private static string Normalize(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return normalized.Length > MaxTextLength ? normalized[..MaxTextLength] : normalized;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var cleaned = new string(name.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "dokument";
        }

        return cleaned.Length > 200 ? cleaned[^200..] : cleaned;
    }
}
