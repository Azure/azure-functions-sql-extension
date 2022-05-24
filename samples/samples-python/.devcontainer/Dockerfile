# Find the Dockerfile at this URL
# https://github.com/Azure/azure-functions-docker/blob/dev/host/4/bullseye/amd64/python/python39/python39-core-tools.Dockerfile
FROM mcr.microsoft.com/azure-functions/python:4-python3.9-core-tools

COPY patch-core-tools.sh /tmp/patch-core-tools.sh
RUN chmod +x /tmp/patch-core-tools.sh
RUN /tmp/patch-core-tools.sh