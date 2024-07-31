CREATE TABLE [dbo].[ProductsWithIdentity] (
    [ProductId] INT            IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (100) NULL,
    [Cost]      INT            NULL,
    PRIMARY KEY CLUSTERED ([ProductId] ASC)
);

GO
ALTER TABLE [dbo].[ProductsWithIdentity] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO