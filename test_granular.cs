using TsqlRefine.Formatting;

var sql = @"
declare @userid int = 1;
declare @username nvarchar(50);

select
    u.userid,
    u.username,
    count(*) as ordercount,
    sum(o.totalamount) as totalspent,
    getdate() as currentdate,
    @@rowcount as rowcount
from dbo.users u
inner join sys.orders o on u.userid = o.userid
where u.isactive = 1
    and o.orderdate >= dateadd(day, -30, getdate())
group by u.userid, u.username;
";

var options = new FormattingOptions
{
    KeywordElementCasing = ElementCasing.Upper,
    BuiltInFunctionCasing = ElementCasing.Upper,
    DataTypeCasing = ElementCasing.Lower,
    SchemaCasing = ElementCasing.Lower,
    TableCasing = ElementCasing.Upper,
    ColumnCasing = ElementCasing.Upper,
    VariableCasing = ElementCasing.Lower
};

var result = SqlFormatter.Format(sql, options);
Console.WriteLine(result);
