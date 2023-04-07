DROP TABLE IF EXISTS [ProductsWithDefaultPK];

CREATE TABLE [ProductsWithDefaultPK] (
	[ProductGuid] [uniqueidentifier] PRIMARY KEY NOT NULL DEFAULT(newsequentialid()),
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL
)