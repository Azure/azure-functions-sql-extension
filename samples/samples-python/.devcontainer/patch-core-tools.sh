#!/bin/sh
mkdir /tmp/patch-core-tools
cd /tmp/patch-core-tools
wget -O localdev.zip https://go.microsoft.com/fwlink/?linkid=2196321
unzip localdev.zip -d core-tools
cp -r core-tools/* /usr/lib/azure-functions-core-tools-4/