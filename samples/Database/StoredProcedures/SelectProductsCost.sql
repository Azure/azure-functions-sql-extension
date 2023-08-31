CREATE PROCEDURE [SelectProductsCost]
	@cost INT
AS
	SELECT [ProductId], [Name], [Cost]
	FROM [dbo].[Products]
	WHERE [Cost] = @cost;