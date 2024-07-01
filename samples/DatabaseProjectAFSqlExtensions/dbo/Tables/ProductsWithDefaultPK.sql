CREATE TABLE [dbo].[ProductsWithDefaultPK] (
    [ProductGuid] UNIQUEIDENTIFIER DEFAULT (newsequentialid()) NOT NULL,
    [Name]        NVARCHAR (100)   NULL,
    [Cost]        INT              NULL,
    PRIMARY KEY CLUSTERED ([ProductGuid] ASC)
);

GO
ALTER TABLE [dbo].[ProductsWithDefaultPK] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO