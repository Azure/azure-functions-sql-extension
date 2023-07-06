// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class ReservedTableNameTrigger
    {
        /// <summary>
        /// Used in verification of the trigger function execution on table with reserved keys as name.
        /// </summary>
        [FunctionName(nameof(ReservedTableNameTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[User]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<User>> changes,
            ILogger logger)
        {
            // The output is used to inspect the trigger binding parameter in test methods.
            logger.LogInformation("SQL Changes: " + Utils.JsonSerializeObject(changes));
        }
    }

    public class User
    {
        public string UserName { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is User)
            {
                var that = obj as User;
                return this.UserId == that.UserId && this.UserName == that.UserName && this.FullName == that.FullName;
            }

            return false;
        }
    }
}
