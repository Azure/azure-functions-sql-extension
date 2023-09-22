CREATE TABLE [dbo].[Products] (
    [ProductId] INT            NOT NULL,
    [Name]      NVARCHAR (100) NULL,
    [Cost]      INT            NULL,
    PRIMARY KEY CLUSTERED ([ProductId] ASC)
);

GO
ALTER TABLE [dbo].[Products] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO