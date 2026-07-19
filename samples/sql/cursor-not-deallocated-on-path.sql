DECLARE items CURSOR FOR SELECT Id FROM dbo.Items;
OPEN items;
CLOSE items;
