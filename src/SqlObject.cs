// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Parses the schema and object name for the given object.
    /// If there is no schema in fullName, SCHEMA_NAME() is returned as schema.
    /// </summary>
    /// <param name="fullName">Full name of object, including schema (if it exists).</param>
    internal class SqlObject
    {
        private static readonly string SCHEMA_NAME_FUNCTION = "SCHEMA_NAME()";

        /// <summary>
        /// The name of the object
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The name of the object, quoted and escaped with single quotes if it's not the default SCHEMA_NAME() function
        /// </summary>
        public readonly string QuotedName;
        /// <summary>
        /// The schema of the object, defaulting to the SCHEMA_NAME() function if the full name doesn't include a schema
        /// </summary>
        public readonly string Schema;
        /// <summary>
        /// The name of the object, quoted and escaped with single quotes
        /// </summary>
        public readonly string QuotedSchema;

        public SqlObject(string fullName)
        {
            var parser = new TSql150Parser(false);
            var stringReader = new StringReader(fullName);
            SchemaObjectName tree = parser.ParseSchemaObjectName(stringReader, out IList<ParseError> errors);

            if (errors.Count > 0)
            {
                string errorMessages = "Encountered error(s) while parsing schema and object name:\n";
                foreach (ParseError err in errors)
                {
                    errorMessages += $"{err.Message}\n";
                }
                throw new InvalidOperationException(errorMessages);
            }

            var visitor = new TSqlObjectFragmentVisitor();
            tree.Accept(visitor);
            this.Schema = visitor.schemaName;
            this.QuotedSchema = this.Schema == SCHEMA_NAME_FUNCTION ? this.Schema : this.Schema.AsQuotedString();
            this.Name = visitor.objectName;
            this.QuotedName = this.Name.AsQuotedString();
        }

        public override string ToString()
        {
            return $"{this.Schema}.{this.Name}";
        }

        /// <summary>
        /// Get the schema and object name from the SchemaObjectName.
        /// </summary>
        private class TSqlObjectFragmentVisitor : SqlServer.TransactSql.ScriptDom.TSqlFragmentVisitor
        {
            public string schemaName;
            public string objectName;

            public override void Visit(SchemaObjectName node)
            {
                this.schemaName = node.SchemaIdentifier != null ? node.SchemaIdentifier.Value : SCHEMA_NAME_FUNCTION;
                this.objectName = node.BaseIdentifier.Value;
            }
        }
    }

}