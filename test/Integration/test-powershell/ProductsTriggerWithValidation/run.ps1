# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.

using namespace System.Net

param($changes)

$expectedMaxBatchSize = $env:TEST_EXPECTED_MAX_BATCH_SIZE
if ($expectedMaxBatchSize -and $expectedMaxBatchSize -ne $changes.Count) {
    throw "Invalid max batch size, got $($changes.Count) changes but expected $expectedMaxBatchSize"
}

$changesJson = $changes | ConvertTo-Json -Compress
Write-Host "SQL Changes: $changesJson"