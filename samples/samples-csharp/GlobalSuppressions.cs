﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// These suppression rules override parameters required by functions binding that cannot be converted to discard variables per issue: https://github.com/Azure/azure-functions-dotnet-worker/issues/323
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsSqlCommand.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Data.SqlClient.SqlCommand)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsString.Run(Microsoft.AspNetCore.Http.HttpRequest,System.String)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsAsyncEnumerable.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IAsyncEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~System.Threading.Tasks.Task{Microsoft.AspNetCore.Mvc.IActionResult}")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsNameEmpty.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsNameNull.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsStoredProcedure.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples.AddProductsAsyncCollector.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.IAsyncCollector{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~System.Threading.Tasks.Task{Microsoft.AspNetCore.Mvc.IActionResult}")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples.AddProductsCollector.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.ICollector{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples.TimerTriggerProducts.Run(Microsoft.Azure.WebJobs.TimerInfo,Microsoft.Extensions.Logging.ILogger,Microsoft.Azure.WebJobs.ICollector{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProducts.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductNamesView.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.ProductName})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsStoredProcedureFromAppSetting.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples.AddProductsWithIdentityColumnArray.Run(Microsoft.AspNetCore.Http.HttpRequest,Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples.ProductWithoutId[]@)~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples.GetProductsTopN.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product})~Microsoft.AspNetCore.Mvc.IActionResult")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Unused parameter is required by functions binding", Scope = "member", Target = "~M:Microsoft.Azure.WebJobs.Extensions.Sql.Samples.MultipleBindingsSamples.GetAndAddProducts.Run(Microsoft.AspNetCore.Http.HttpRequest,System.Collections.Generic.IEnumerable{Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product},Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common.Product[]@)~Microsoft.AspNetCore.Mvc.IActionResult")]
