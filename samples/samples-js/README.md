## Run Host

To run the function host locally follow these steps :

1. Run `func extensions install --package Microsoft.Azure.WebJobs.Extensions.Sql --version 0.1.286-preview`
2. Run `func extensions sync` (this is only needed for a clean build, you can skip this later on unless you clean the output folders or change package versions)
3. Copy `bin/bin/extensions.json` to `bin/extensions.json`
4. Copy `bin/bin/function.deps.json` to `bin/function.deps.json`
5. Run `func start`