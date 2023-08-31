DROP TABLE IF EXISTS [ProductsWithUnsupportedColumnTypes];

CREATE TABLE [ProductsWithUnsupportedColumnTypes] (
    [ProductId] [int] NOT NULL PRIMARY KEY,
    [Name] [nvarchar](100) NULL,
    [Cost] [int] NULL,
    [Location] [geography] NULL,
    [Geometry] [geometry] NULL,
    [Organization] [hierarchyid] NULL
);