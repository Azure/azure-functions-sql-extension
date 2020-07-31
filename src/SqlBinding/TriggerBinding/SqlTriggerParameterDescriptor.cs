using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>
        /// Name of the table being monitored
        /// </summary>
        public string TableName { get; set; }

        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return string.Format("New changes on table {0} at {1}", this.TableName, DateTime.UtcNow.ToString("o"));
        }
    }
}
