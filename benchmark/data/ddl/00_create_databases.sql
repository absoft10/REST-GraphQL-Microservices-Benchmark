-- 00_create_databases.sql — three service-owned databases (DECISION-002 AP-6, AD-11).
-- Run against [master]. sqlcmd variables: $(CatalogDb), $(MembersDb), $(LoansDb).
-- Collation pinned to SQL_Latin1_General_CP1_CI_AS (LocalDB server default and RDS SQL Server default),
-- compatibility level pinned to 160 (SQL Server 2022 semantics; supported by LocalDB 17.0 and RDS 2022).
IF DB_ID(N'$(CatalogDb)') IS NULL CREATE DATABASE [$(CatalogDb)] COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
IF DB_ID(N'$(MembersDb)') IS NULL CREATE DATABASE [$(MembersDb)] COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
IF DB_ID(N'$(LoansDb)') IS NULL CREATE DATABASE [$(LoansDb)] COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
ALTER DATABASE [$(CatalogDb)] SET COMPATIBILITY_LEVEL = 160;
ALTER DATABASE [$(MembersDb)] SET COMPATIBILITY_LEVEL = 160;
ALTER DATABASE [$(LoansDb)] SET COMPATIBILITY_LEVEL = 160;
GO
