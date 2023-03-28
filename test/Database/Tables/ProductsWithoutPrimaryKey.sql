DROP TABLE IF EXISTS [ProductsWithoutPrimaryKey];

CREATE TABLE [ProductsWithoutPrimaryKey] (
    [ProductId] [int] NOT NULL,
    [Name] [nvarchar](100) NULL,
    [Cost] [int] NULL
);