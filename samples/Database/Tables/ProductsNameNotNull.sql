DROP TABLE IF EXISTS [ProductsNameNotNull];

CREATE TABLE [ProductsNameNotNull] (
	[ProductId] [int] NOT NULL PRIMARY KEY,
	[Name] [nvarchar](100) NOT NULL,
	[Cost] [int] NULL
)