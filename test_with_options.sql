declare @userid int = 1;
select u.userid, count(*) as total, getdate() from dbo.users u where u.isactive = 1;
