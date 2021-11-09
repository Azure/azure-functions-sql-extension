// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// These suppression rules override parameters required by functions binding that cannot be converted to discard variables per issue: https://github.com/Azure/azure-functions-dotnet-worker/issues/323
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration.AddProductExtraColumns.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common.ProductExtraColumns@)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration.AddProductMissingColumns.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common.ProductMissingColumns@)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration.AddProductMissingColumnsExceptionFunction.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common.ProductMissingColumns@)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration.AddProductsNoPartialUpsert.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.ICollector{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
