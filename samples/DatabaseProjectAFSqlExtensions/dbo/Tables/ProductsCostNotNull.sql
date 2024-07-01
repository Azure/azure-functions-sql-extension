CREATE TABLE [dbo].[ProductsCostNotNull] (
    [ProductId] INT            NOT NULL,
    [Name]      NVARCHAR (100) NULL,
    [Cost]      INT            NOT NULL,
    PRIMARY KEY CLUSTERED ([ProductId] ASC)
);

GO
ALTER TABLE [dbo].[ProductsCostNotNull] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO