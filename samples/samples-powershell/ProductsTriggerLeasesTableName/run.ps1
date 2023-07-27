# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

param($changes)

$changesJson = $changes | ConvertTo-Json -Compress
Write-Host "SQL Changes: $changesJson"