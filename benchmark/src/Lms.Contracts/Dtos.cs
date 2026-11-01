namespace Lms.Contracts;

// Full resource representations (docs/api-contracts/rest-api.md §1; DECISION-002 AP-7, DECISION-004 O-4).
// Property declaration order = JSON property order.

/// <summary>Book full representation. Field set is PROVISIONAL until the Kaggle dataset is verified (O-7).</summary>
public sealed record BookDto(
    int BookId,
    string Title,
    string Authors,
    string? Description,
    string? Category,
    string? Publisher,
    decimal? Price,
    string? PublishMonth,
    int? PublishYear);

/// <summary>Member full representation: exactly the baseline User fields (O-4).</summary>
public sealed record MemberDto(
    int MemberId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address);

/// <summary>Loan full representation: exactly the baseline Loan fields (O-4).</summary>
public sealed record LoanDto(
    int LoanId,
    int MemberId,
    int BookId,
    DateOnly LoanDate,
    DateOnly? ReturnDate,
    bool IsReturned);

/// <summary>N-RC composite: loan full representation plus embedded book (AD-10).</summary>
public sealed record LoanWithBookDto(
    int LoanId,
    int MemberId,
    int BookId,
    DateOnly LoanDate,
    DateOnly? ReturnDate,
    bool IsReturned,
    BookDto Book);

/// <summary>N-RC composite: member full representation plus embedded loans (AD-10).</summary>
public sealed record MemberWithLoansDto(
    int MemberId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string Address,
    IReadOnlyList<LoanWithBookDto> Loans);
