# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

param($changes)
$changesJson = $changes | ConvertTo-Json
# The output is used to inspect the trigger binding parameter in test methods.
# Removing new lines for testing purposes.
$changesJson = $changesJson -replace [Environment]::NewLine,"";
Write-Host "Trigger2 Changes: $changesJson"