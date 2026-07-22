# exec-parameter-function-call

## Summary

Detects function calls passed directly as arguments to `EXEC` or `EXECUTE` procedure calls.
SQL Server rejects this form; assign the function result to a variable before passing it.

## Examples

```sql
EXECUTE Proc1 @date = GETDATE(); -- invalid
EXEC Proc1 ABS(-1);              -- invalid

DECLARE @date datetime = GETDATE();
EXECUTE Proc1 @date = @date;     -- valid
```

The rule covers named and positional arguments, including built-in, conversion, parsing, and
user-defined function call forms. It only reports arguments whose root expression is a function
call.

## Category

Correctness

## Severity

Error

## Fixable

No
