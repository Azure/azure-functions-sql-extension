CREATE TABLE [dbo].[ProductsWithMultiplePrimaryColumnsAndIdentity] (
    [ProductId]  INT            IDENTITY (1, 1) NOT NULL,
    [ExternalId] INT            NOT NULL,
    [Name]       NVARCHAR (100) NULL,
    [Cost]       INT            NULL,
    PRIMARY KEY CLUSTERED ([ProductId] ASC, [ExternalId] ASC)
);

GO
ALTER TABLE [dbo].[ProductsWithMultiplePrimaryColumnsAndIdentity] ENABLE CHANGE_TRACKING WITH (TRACK_COLUMNS_UPDATED = ON);

GO