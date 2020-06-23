using Microsoft.Azure.WebJobs;
using System;

namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    public static class SQLBindingExtension
    {
        public static IWebJobsBuilder AddSQLBinding(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<SQLBindingConfigProvider>();
            return builder;
        }
    }
}