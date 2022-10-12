// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// These suppression rules override parameters required by functions binding that cannot be converted to discard variables per issue: https://github.com/Azure/azure-functions-dotnet-worker/issues/323
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.GetProducts.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
