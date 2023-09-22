CREATE TABLE [dbo].[ProductsNameNotNull] (
    [ProductId] INT            NOT NULL,
    [Name]      NVARCHAR (100) NOT NULL,
    [Cost]      INT            NULL,
    PRIMARY KEY CLUSTERED ([ProductId] ASC)
);

GO
ALTER TABLE [dbo].[ProductsNameNotNull] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO