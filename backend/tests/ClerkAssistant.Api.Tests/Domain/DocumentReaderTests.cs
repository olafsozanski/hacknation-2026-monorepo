using System.IO.Compression;
using System.Text;
using ClerkAssistant.Api.Infrastructure.Documents;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;

namespace ClerkAssistant.Api.Tests.Domain;

public class DocumentReaderTests
{
    private readonly DocumentReader _reader = new();

    [Fact]
    public async Task Reads_utf8_text_file()
    {
        var document = await _reader.ReadAsync(
            FormFile("odpowiedz.txt", Encoding.UTF8.GetBytes("  Zażółć gęślą jaźń.\r\nDruga linia.  ")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Zażółć gęślą jaźń.\nDruga linia.", document.Text);
        Assert.StartsWith("text/plain", document.ContentType);
        Assert.True(document.HasText);
    }

    [Fact]
    public async Task Reads_docx_paragraphs()
    {
        var document = await _reader.ReadAsync(FormFile("wniosek.docx", BuildDocx("Wniosek o udostępnienie", "informacji publicznej")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Wniosek o udostępnienie\ninformacji publicznej", document.Text);
    }

    [Fact]
    public async Task Rejects_pdf_with_wrong_signature()
    {
        var error = await Assert.ThrowsAsync<DocumentRejectedException>(() =>
            _reader.ReadAsync(FormFile("skan.pdf", "To nie jest PDF"u8.ToArray()), TestContext.Current.CancellationToken));

        Assert.Contains("PDF", error.Message);
    }

    [Fact]
    public async Task Rejects_legacy_doc_format_with_hint()
    {
        var error = await Assert.ThrowsAsync<DocumentRejectedException>(() =>
            _reader.ReadAsync(FormFile("pismo.doc", [0xD0, 0xCF, 0x11, 0xE0]), TestContext.Current.CancellationToken));

        Assert.Contains(".doc", error.Message);
    }

    [Fact]
    public async Task Rejects_empty_file()
    {
        await Assert.ThrowsAsync<DocumentRejectedException>(() =>
            _reader.ReadAsync(FormFile("pusty.txt", []), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rejects_docx_with_too_many_archive_entries()
    {
        var error = await Assert.ThrowsAsync<DocumentRejectedException>(() =>
            _reader.ReadAsync(FormFile("bomba.docx", BuildZip(2001)), TestContext.Current.CancellationToken));

        Assert.Contains("podejrzaną strukturę", error.Message);
    }

    private static FormFile FormFile(string fileName, byte[] content) =>
        new(new MemoryStream(content), 0, content.Length, "file", fileName);

    private static byte[] BuildDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p))))));
        }

        return stream.ToArray();
    }

    private static byte[] BuildZip(int extraEntries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("word/document.xml");
            for (var i = 0; i < extraEntries; i++)
            {
                zip.CreateEntry($"customXml/item{i}.xml");
            }
        }

        return stream.ToArray();
    }
}
