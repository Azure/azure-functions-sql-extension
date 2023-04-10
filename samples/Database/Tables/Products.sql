DROP TABLE IF EXISTS [Products];

CREATE TABLE [Products] (
	[ProductId] [int] NOT NULL PRIMARY KEY,
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL
)