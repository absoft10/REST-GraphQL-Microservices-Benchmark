-- 30_loans.sql — Loans service database. Run against [$(LoansDb)].
-- Columns: exactly the baseline Loan fields (DECISION-004, O-4) — approved.
-- member_id / book_id are LOGICAL references to other services' databases (no cross-database FKs);
-- integrity is enforced by the generator and verified by `lms-bench db verify` (doc 03 §2.4).
IF SCHEMA_ID(N'loans') IS NULL EXEC(N'CREATE SCHEMA loans');
GO
IF SCHEMA_ID(N'meta') IS NULL EXEC(N'CREATE SCHEMA meta');
GO
IF OBJECT_ID(N'loans.loan', N'U') IS NULL
CREATE TABLE loans.loan (
    loan_id     INT  NOT NULL CONSTRAINT pk_loan PRIMARY KEY CLUSTERED,
    member_id   INT  NOT NULL,
    book_id     INT  NOT NULL,
    loan_date   DATE NOT NULL,
    return_date DATE NULL,
    is_returned BIT  NOT NULL,
    CONSTRAINT ck_loan_return CHECK ((is_returned = 1 AND return_date IS NOT NULL) OR (is_returned = 0 AND return_date IS NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_loan_member' AND object_id = OBJECT_ID(N'loans.loan'))
CREATE NONCLUSTERED INDEX ix_loan_member ON loans.loan (member_id, loan_id)
    INCLUDE (book_id, loan_date, return_date, is_returned);
GO
IF OBJECT_ID(N'meta.dataset_info', N'U') IS NULL
CREATE TABLE meta.dataset_info (
    info_key   NVARCHAR(100)  NOT NULL CONSTRAINT pk_dataset_info PRIMARY KEY,
    info_value NVARCHAR(4000) NOT NULL
);
GO
