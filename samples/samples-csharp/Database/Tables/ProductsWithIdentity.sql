DROP TABLE IF EXISTS [ProductsWithIdentity];

CREATE TABLE [ProductsWithIdentity] (
	[ProductId] [int] PRIMARY KEY NOT NULL IDENTITY(1,1),
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL
)