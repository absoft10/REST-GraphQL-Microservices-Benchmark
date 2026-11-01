using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Lms.Contracts;

namespace Lms.Data.Dataset;

/// <summary>
/// Pinned research source for the catalog (DECISION-009; profiling report docs/milestone2/09 §1–§2).
/// Changing any value here changes the research dataset and requires a new decision record.
/// </summary>
public static class KaggleBooksSource
{
    public const string DatasetRef = "elvinrustam/books-dataset";
    public const int KaggleVersion = 3;
    public const string ArchiveSha256 = "e42f3dc8a64ed4a549bcf956b4f712452978ede7aa8ce49d3cdd27a3bf831839";
    public const string EntryName = "BooksDatasetClean.csv";
    public const string EntrySha256 = "e59f031f238b44b7c470ec4926e1a5c231b5cf00be9923308c19e577dd4bda53";
    public const string RulesVersion = "kaggle-books-clean-rules/1.0 (R1-R9, docs/milestone2/09 §6)";
    public const string SourceLabel = "KAGGLE_elvinrustam_books-dataset_v3";
    public const string DefaultArchivePath = "data/source/kaggle-books-dataset-v3/archive.zip";

    public static readonly string[] Header =
    [
        "Title", "Authors", "Description", "Category", "Publisher",
        "Price Starting With ($)", "Publish Date (Month)", "Publish Date (Year)",
    ];
}

/// <summary>Counts of what the deterministic rules did (reported in the manifest; no row is ever dropped).</summary>
public sealed record BooksImportStats(
    long SourceRows,
    long ValuesChangedByNormalization,
    long NullDescription, long NullCategory, long NullPublisher, long NullPrice, long NullPublishMonth, long NullPublishYear,
    long AuthorsBarePlaceholderBy,
    long PricesWithOneDecimalInSource);

public sealed record BooksSourceInfo(
    string DatasetRef, int KaggleVersion, string ArchiveFileName, string ArchiveSha256,
    string EntryName, string EntrySha256, string RulesVersion, BooksImportStats Stats);

public sealed record BooksImport(IReadOnlyList<BookDto> Books, BooksSourceInfo Info);

/// <summary>
/// Implements the approved cleaning rules R1–R9 (docs/milestone2/09 §6; DECISION-009) and nothing else:
/// R1 strict UTF-8, pinned header and field count; R2 book_id = 1-based data-row order; R3 NFC then trim of
/// Unicode white space at both ends; R4 empty optional value → NULL; R5 exact decimal price, error if rounding
/// would be needed; R6 integer year, verbatim month; R7 no exclusion, de-duplication, HTML decoding, mojibake
/// repair, whitespace collapsing, category splitting or case change; R8 authors verbatim (DS-5 option A,
/// "By" placeholders kept); R9 source hashes and rule version recorded.
/// </summary>
public static class KaggleBooksImporter
{
    public static BooksImport Import(string archivePath)
    {
        var archiveBytes = File.ReadAllBytes(archivePath);
        var archiveSha = CanonicalEncoding.Sha256Hex(archiveBytes);
        if (archiveSha != KaggleBooksSource.ArchiveSha256)
            throw new InvalidDataException($"Archive SHA-256 {archiveSha} does not match the pinned research source {KaggleBooksSource.ArchiveSha256}.");

        using var zip = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
        var entry = zip.GetEntry(KaggleBooksSource.EntryName)
                    ?? throw new InvalidDataException($"Archive does not contain {KaggleBooksSource.EntryName}.");
        byte[] csvBytes;
        using (var s = entry.Open())
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            csvBytes = ms.ToArray();
        }
        var entrySha = CanonicalEncoding.Sha256Hex(csvBytes);
        if (entrySha != KaggleBooksSource.EntrySha256)
            throw new InvalidDataException($"{KaggleBooksSource.EntryName} SHA-256 {entrySha} does not match the pinned value.");

        var (books, stats) = Parse(csvBytes);
        return new BooksImport(books, new BooksSourceInfo(KaggleBooksSource.DatasetRef, KaggleBooksSource.KaggleVersion,
            Path.GetFileName(archivePath), archiveSha, KaggleBooksSource.EntryName, entrySha, KaggleBooksSource.RulesVersion, stats));
    }

    /// <summary>Rules R1–R8 on raw CSV bytes (exposed for tests on synthetic inputs).</summary>
    public static (List<BookDto> Books, BooksImportStats Stats) Parse(byte[] csvBytes)
    {
        // R1: strict UTF-8 (throws on invalid sequences); a BOM would break the pinned header and is rejected.
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(csvBytes);
        if (text.Length > 0 && text[0] == '﻿') throw new InvalidDataException("Unexpected byte-order mark.");

        using var records = Rfc4180Reader.Read(text).GetEnumerator();
        if (!records.MoveNext() || !records.Current.SequenceEqual(KaggleBooksSource.Header))
            throw new InvalidDataException("Header does not match the pinned header: " + string.Join(" | ", KaggleBooksSource.Header));

        var books = new List<BookDto>(110_000);
        long changed = 0, nDesc = 0, nCat = 0, nPub = 0, nPrice = 0, nMonth = 0, nYear = 0, bareBy = 0, oneDecimal = 0;
        while (records.MoveNext())
        {
            var f = records.Current;
            var rowNumber = books.Count + 1; // R2
            if (f.Length != KaggleBooksSource.Header.Length)
                throw new InvalidDataException($"Data row {rowNumber}: expected {KaggleBooksSource.Header.Length} fields, found {f.Length}.");

            string? Text(int i, bool required, ref long nulls)
            {
                var v = f[i].Normalize(NormalizationForm.FormC).Trim(); // R3: NFC, then trim Unicode white space
                if (v != f[i] && f[i].Trim().Length > 0) changed++;
                if (v.Length > 0) return v;
                if (required) throw new InvalidDataException($"Data row {rowNumber}: required column '{KaggleBooksSource.Header[i]}' is empty.");
                nulls++;                                                 // R4
                return null;
            }

            long none = 0;
            var title = Text(0, true, ref none)!;
            var authors = Text(1, true, ref none)!;                    // R8: verbatim (after R3), "By" kept
            if (authors == "By") bareBy++;
            var description = Text(2, false, ref nDesc);
            var category = Text(3, false, ref nCat);                    // DS-6: trim only, hierarchy string kept
            var publisher = Text(4, false, ref nPub);
            var priceToken = Text(5, false, ref nPrice);
            var month = Text(6, false, ref nMonth);                     // R6: verbatim
            var yearToken = Text(7, false, ref nYear);

            decimal? price = null;                                      // R5
            if (priceToken is not null)
            {
                if (!decimal.TryParse(priceToken, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var p))
                    throw new InvalidDataException($"Data row {rowNumber}: price '{priceToken}' is not a plain decimal.");
                if (decimal.Round(p, 2) != p) throw new InvalidDataException($"Data row {rowNumber}: price '{priceToken}' would need rounding.");
                if (p >= 100_000_000m) throw new InvalidDataException($"Data row {rowNumber}: price '{priceToken}' exceeds DECIMAL(10,2).");
                if (priceToken.Contains('.') && priceToken.Length - priceToken.IndexOf('.') - 1 == 1) oneDecimal++;
                price = p + 0.00m;                                      // scale 2, value unchanged
            }

            int? year = null;                                           // R6
            if (yearToken is not null)
            {
                if (!int.TryParse(yearToken, NumberStyles.None, CultureInfo.InvariantCulture, out var y) || y > short.MaxValue)
                    throw new InvalidDataException($"Data row {rowNumber}: year '{yearToken}' is not a SMALLINT integer.");
                year = y;
            }

            books.Add(new BookDto(rowNumber, title, authors, description, category, publisher, price, month, year));
        }
        return (books, new BooksImportStats(books.Count, changed, nDesc, nCat, nPub, nPrice, nMonth, nYear, bareBy, oneDecimal));
    }
}
