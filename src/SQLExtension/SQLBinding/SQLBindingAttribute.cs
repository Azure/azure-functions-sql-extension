using Microsoft.Azure.WebJobs.Description;
using System;
using System.Collections.Generic;
using System.Text;

namespace SQLBinding
{
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public class SQLBindingAttribute : Attribute
    {
        public string ConnectionString { get; set; }

        [AutoResolve]
        public string SQLQuery { get; set; }

        [AutoResolve]
        public string Authentication { get; set; }
    }
}
