using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public static class SqlBindingExtension
    {
        public static IWebJobsBuilder AddSqlBinding(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<SqlBindingConfigProvider>();
            return builder;
        }
    }
}