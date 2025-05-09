# Description

*Please provide a detailed description. Be as descriptive as possible - include information about what is being changed,
why it's being changed, and any links to relevant issues. If this is closing an existing issue use one of the [issue linking keywords](https://docs.github.com/issues/tracking-your-work-with-issues/using-issues/linking-a-pull-request-to-an-issue#linking-a-pull-request-to-an-issue-using-a-keyword) to link the issue to this PR and have it automatically close when completed.*

In addition, go through the checklist below and check each item as you validate it is either handled or not applicable to this change.

# Code Changes

- [ ] [Unit tests](https://github.com/Azure/azure-functions-sql-extension/tree/main/test/Unit) are added, if possible
- [ ] [integration tests](https://github.com/Azure/azure-functions-sql-extension/tree/main/test/Integration)  are addedif the change is modifying existing behavior of one or more of the bindings
- [ ] New or changed code follows the C# style guidelines defined in .editorconfig
- [ ] All changes MUST be backwards compatible and changes to the shared `az_func.GlobalState` table must be comptible with all prior versions of the extension
- [ ] Use the `ILogger` instance to log relevant information, especially information useful for debugging or troubleshooting
- [ ] Use `async` and `await` for all long-running operations
- [ ] Ensure proper usage and propagation of `CancellationToken`
- [ ] T-SQL is safe from SQL Injection attacks through the use of [SqlParameters](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlparameter) and proper escaping/sanitization of input

# Dependencies

- [ ] If updating dependencies, run `dotnet restore --force-evaluate` to update the lock files and ensure that there are NO major versions updates in either [src/packages.lock.json](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/packages.lock.json) or [Worker.Extensions.Sql/src/pacakkag.lock.json](https://github.com/Azure/azure-functions-sql-extension/blob/main/Worker.Extensions.Sql/src/packages.lock.json). If there are, contact the dev team for instructions.

# Documentation

- [ ] Add [samples](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples) if the change is modifying or adding functionality
- [ ] Update relevant documentation in the [docs](https://github.com/Azure/azure-functions-sql-extension/tree/main/docs)
