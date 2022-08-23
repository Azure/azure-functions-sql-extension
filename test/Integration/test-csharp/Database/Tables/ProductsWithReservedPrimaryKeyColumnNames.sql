CREATE TABLE [ProductsWithReservedPrimaryKeyColumnNames] (
	[ProductId] [int] NOT NULL IDENTITY(1, 1),
	[ChangeVersion] [int] NOT NULL,
	[AttemptCount] [int] NOT NULL,
	[LeaseExpirationTime] [int] NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL,
	PRIMARY KEY (
		ProductId,
		ChangeVersion,
		AttemptCount,
		LeaseExpirationTime
	)
);