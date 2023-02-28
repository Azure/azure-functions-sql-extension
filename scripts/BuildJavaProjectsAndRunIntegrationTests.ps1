# Run this script from the root of the repository to build the
# samples-java and test-java Azure Function projects and run the
# Integration tests.

# Maven is required to build the Java projects.
# Instructions to download Maven can be found here:
# https://github.com/Microsoft/vscode-azurefunctions/wiki/Configure-Maven

# Build the samples-java project
cd ./samples/samples-java
mvn clean package

# Build the test-java project
cd ../../test/Integration/test-java
mvn clean package

# Run the Integration tests
cd ../../..
dotnet test