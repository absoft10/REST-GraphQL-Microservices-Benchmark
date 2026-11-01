-- 20_members.sql — Members service database. Run against [$(MembersDb)].
-- Columns: exactly the baseline User fields (DECISION-004, O-4) — approved.
IF SCHEMA_ID(N'members') IS NULL EXEC(N'CREATE SCHEMA members');
GO
IF SCHEMA_ID(N'meta') IS NULL EXEC(N'CREATE SCHEMA meta');
GO
IF OBJECT_ID(N'members.member', N'U') IS NULL
CREATE TABLE members.member (
    member_id    INT            NOT NULL CONSTRAINT pk_member PRIMARY KEY CLUSTERED,
    first_name   NVARCHAR(100)  NOT NULL,
    last_name    NVARCHAR(100)  NOT NULL,
    email        NVARCHAR(200)  NOT NULL,
    phone_number NVARCHAR(40)   NOT NULL,
    address      NVARCHAR(300)  NOT NULL
);
GO
IF OBJECT_ID(N'meta.dataset_info', N'U') IS NULL
CREATE TABLE meta.dataset_info (
    info_key   NVARCHAR(100)  NOT NULL CONSTRAINT pk_dataset_info PRIMARY KEY,
    info_value NVARCHAR(4000) NOT NULL
);
GO
