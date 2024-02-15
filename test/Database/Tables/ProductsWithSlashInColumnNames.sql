DROP TABLE IF EXISTS [ProductsWithSlashInColumnNames];

CREATE TABLE [ProductsWithSlashInColumnNames] (
    [ProductId] [int] NOT NULL PRIMARY KEY,
    [Name/Test] [nchar](100) NOT NULL,
	[Cost/Test] [int] NOT NULL
)
