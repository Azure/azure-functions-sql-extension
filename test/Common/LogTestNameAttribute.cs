// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Xunit.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    internal class LogTestNameAttribute : BeforeAfterTestAttribute
    {
        public override void Before(MethodInfo methodUnderTest)
        {
            Console.WriteLine($"Starting test {methodUnderTest.Name}");
        }

        public override void After(MethodInfo methodUnderTest)
        {
            Console.WriteLine($"Completed test {methodUnderTest.Name}");
        }
    }
}
