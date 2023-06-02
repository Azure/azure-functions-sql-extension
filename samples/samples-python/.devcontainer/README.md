# Python devcontainer for SQL bindings public preview

The use of SQL bindings during the early public preview phase requires additional setup for local development due to the requirement of the Azure Functions host 4.5.0 or greater.  With the included files (`devcontainer.json`, `Dockerfile`, and `patch-core-tools.sh`), your environment can be automatically setup to support development for SQL bindings in VS Code remote containers or Codespaces.  Once inside this environment, your VS Code tasks (F5 to run) will perform as expected.

For more on remote development in VS Code, please check out their [documentation](https://code.visualstudio.com/docs/remote/containers).


## What does this environment include?

This environment definition takes the standard Python 3.9 and Azure Functions development environment image as its base image, then applies an update to a pre-release version of the [Azure Functions Core Tools](https://github.com/azure/azure-functions-core-tools).

The update to Azure Functions Core Tools takes place in [patch-core-tools.sh](patch-core-tools.sh).