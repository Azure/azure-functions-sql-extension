DROP TABLE IF EXISTS [ProductsUnsupportedTypes];

CREATE TABLE [ProductsUnsupportedTypes] (
    [ProductId] [int] NOT NULL PRIMARY KEY,
    [TextCol] [text],
    [NtextCol] [ntext],
    [ImageCol] [image]
)