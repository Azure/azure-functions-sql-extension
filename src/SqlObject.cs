// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Helper class for working with SQL object names.
    /// </summary>
    internal class SqlObject
    {
        private static readonly string SCHEMA_NAME_FUNCTION = "SCHEMA_NAME()";

        /// <summary>
        /// The name of the object
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// The name of the object, quoted and escaped with single quotes
        /// </summary>
        public readonly string QuotedName;
        /// <summary>
        /// The schema of the object, defaulting to the SCHEMA_NAME() function if the full name doesn't include a schema
        /// </summary>
        public readonly string Schema;
        /// <summary>
        /// The name of the object, quoted and escaped with single quotes if it's not the default SCHEMA_NAME() function
        /// </summary>
        public readonly string QuotedSchema;
        /// <summary>
        /// The full name of the object in the format SCHEMA.NAME (or just NAME if there is no specified schema)
        /// </summary>
        public readonly string FullName;
        /// <summary>
        /// The full name of the object in the format SCHEMA.NAME (or just NAME if there is no specified schema), quoted and escaped with single quotes
        /// </summary>
        public readonly string QuotedFullName;

        /// <summary>
        /// A SqlObject which contains information about the name and schema of the given object full name.
        /// </summary>
        /// <param name="fullName">Full name of object, including schema (if it exists).</param>
        /// <exception cref="InvalidOperationException">If the name can't be parsed</exception>
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
            this.QuotedSchema = this.Schema == SCHEMA_NAME_FUNCTION ? this.Schema : this.Schema.AsSingleQuotedString();
            this.Name = visitor.objectName;
            this.QuotedName = this.Name.AsSingleQuotedString();
            this.FullName = this.Schema == SCHEMA_NAME_FUNCTION ? this.Name : $"{this.Schema}.{this.Name}";
            this.QuotedFullName = $"'{this.FullName.AsSingleQuoteEscapedString()}'";
        }

        /// <summary>
        /// Returns the full name of the object, including the schema if it was specified, in the format {Schema}.{Name}
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{this.Schema}.{this.Name}";
        }

        /// <summary>
        /// Get the schema and object name from the SchemaObjectName.
        /// </summary>
        private class TSqlObjectFragmentVisitor : TSqlFragmentVisitor
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