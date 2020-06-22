using Microsoft.Azure.WebJobs;
using System;

namespace SQLBinding
{
    public static class SQLBindingExtension
    {
        public static IWebJobsBuilder AddSQLBinding(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<SQLBinding>();
            return builder;
        }
    }
}