-- 10_catalog.sql — Catalog service database. Run against [$(CatalogDb)].
-- !!! catalog.book is PROVISIONAL: column set follows docs/api-contracts/rest-api.md §1.1 and will be
-- !!! finalised only after the Kaggle dataset has been downloaded and verified (DECISION-004, O-7).
IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA catalog');
GO
IF SCHEMA_ID(N'meta') IS NULL EXEC(N'CREATE SCHEMA meta');
GO
IF OBJECT_ID(N'catalog.book', N'U') IS NULL
CREATE TABLE catalog.book (
    book_id        INT            NOT NULL CONSTRAINT pk_book PRIMARY KEY CLUSTERED,
    title          NVARCHAR(400)  NOT NULL,
    authors        NVARCHAR(400)  NOT NULL,
    description    NVARCHAR(MAX)  NULL,
    category       NVARCHAR(200)  NULL,
    publisher      NVARCHAR(200)  NULL,
    price          DECIMAL(10,2)  NULL,
    publish_month  NVARCHAR(20)   NULL,
    publish_year   SMALLINT       NULL
);
GO
-- Provenance of the loaded data; read by tools only, never exposed by the APIs.
IF OBJECT_ID(N'meta.dataset_info', N'U') IS NULL
CREATE TABLE meta.dataset_info (
    info_key   NVARCHAR(100)  NOT NULL CONSTRAINT pk_dataset_info PRIMARY KEY,
    info_value NVARCHAR(4000) NOT NULL
);
GO
