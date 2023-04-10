DROP TABLE IF EXISTS [ProductsWithMultiplePrimaryColumnsAndIdentity];

CREATE TABLE [ProductsWithMultiplePrimaryColumnsAndIdentity] (
	[ProductId] [int] NOT NULL IDENTITY(1,1),
	[ExternalId] [int] NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL,
	PRIMARY KEY (ProductId, ExternalId)
)