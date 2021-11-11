# Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the Microsoft Open Source Code of Conduct. For more information see the Code of Conduct FAQ or contact opencode@microsoft.com with any additional questions or comments.

<br>

## Contributor Getting Started

### SQL Setup

In order to test changes it is suggested that you have a SQL server set up to connect to and run queries against. Instructions to set this up can be found in the [Quick Start Guide](./README.md#quick-start)

### Set Up Development Environment

1. [Install VS Code](https://code.visualstudio.com/Download)

2. Clone repo and open in VS Code:

```bash
git clone https://github.com/Azure/azure-functions-sql-extension
cd azure-functions-sql-extension
code .
```
3. Install extensions when prompted after VS Code opens
   - Note: This includes the Azure Functions, C#, and editorconfig extensions

4. Configure the Function App located in the [samples](./samples) folder by following the instructions [here](./README.md#configure-function-app)

5. Press F5 to run SQL bindings samples that are included in this repo. The output window should display startup information as well as the function endpoints that were started.