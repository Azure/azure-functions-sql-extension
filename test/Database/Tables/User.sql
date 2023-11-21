DROP TABLE IF EXISTS [dbo].[User];

CREATE TABLE [dbo].[User] (
    [UserId] [int] NOT NULL PRIMARY KEY,
    [UserName] [nvarchar](50) NOT NULL,
	[FullName] [nvarchar](max) NULL
)
