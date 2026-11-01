using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lms.Contracts;

namespace Lms.Data.Dataset;

/// <summary>
/// Canonical row encoding for data-freeze content hashes (doc 03 §2.4, AD-36).
/// One line per row (rows ordered by primary key), fields in DDL column order separated by U+001F,
/// NULL encoded as U+0000, strings as stored (NFC), DATE as yyyy-MM-dd, DECIMAL(10,2) with exactly two decimals,
/// integers invariant, BIT as 0/1. The generator and the database verifier both use this code path.
/// </summary>
public static class CanonicalEncoding
{
    private const char Sep = '\u001F';
    private const char Null = '\u0000';

    public static string HashBooks(IEnumerable<BookDto> rows) => Hash(rows.OrderBy(r => r.BookId), r =>
        [I(r.BookId), S(r.Title), S(r.Authors), S(r.Description), S(r.Category), S(r.Publisher), D2(r.Price), S(r.PublishMonth), I(r.PublishYear)]);

    public static string HashMembers(IEnumerable<MemberDto> rows) => Hash(rows.OrderBy(r => r.MemberId), r =>
        [I(r.MemberId), S(r.FirstName), S(r.LastName), S(r.Email), S(r.PhoneNumber), S(r.Address)]);

    public static string HashLoans(IEnumerable<LoanDto> rows) => Hash(rows.OrderBy(r => r.LoanId), r =>
        [I(r.LoanId), I(r.MemberId), I(r.BookId), Date(r.LoanDate), Date(r.ReturnDate), r.IsReturned ? "1" : "0"]);

    public static string Sha256Hex(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static string Hash<T>(IEnumerable<T> rows, Func<T, string[]> fields)
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.Clear();
            sb.AppendJoin(Sep, fields(row)).Append('\n');
            h.AppendData(Encoding.UTF8.GetBytes(sb.ToString()));
        }
        return Convert.ToHexStringLower(h.GetHashAndReset());
    }

    private static string S(string? v) => v ?? Null.ToString();
    private static string I(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? Null.ToString();
    private static string D2(decimal? v) => v?.ToString("0.00", CultureInfo.InvariantCulture) ?? Null.ToString();
    private static string Date(DateOnly? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? Null.ToString();
}
