DROP TABLE IF EXISTS [ProductsWithReservedPrimaryKeyColumnNames];

CREATE TABLE [ProductsWithReservedPrimaryKeyColumnNames] (
	[ProductId] [int] NOT NULL IDENTITY(1, 1),
	[_az_func_ChangeVersion] [int] NOT NULL,
	[_az_func_AttemptCount] [int] NOT NULL,
	[_az_func_LeaseExpirationTime] [int] NOT NULL,
	[Name] [nvarchar](100) NULL,
	[Cost] [int] NULL,
	PRIMARY KEY (
		ProductId,
		_az_func_ChangeVersion,
		_az_func_AttemptCount,
		_az_func_LeaseExpirationTime
	)
);