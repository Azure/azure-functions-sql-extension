// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using Xunit.Sdk;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <summary>
    /// SqlInlineData attribute class for adding language parameter data to tests. This allows
    /// tests to be run against all the supported languages list that are specified in the SupportedLanguages
    /// list of this class (ex: CSharp, JavaScript)
    /// </summary>
    public class SqlInlineDataAttribute : DataAttribute
    {
        private readonly List<object[]> testData = new List<object[]>();

        /// <summary>
        /// Adds a language parameter to the test data which will contain the language 
        /// that the test is currently running against from the list of supported languages
        /// </summary>
        /// <param name="args">The test parameters to insert inline for the test</param>
        public SqlInlineDataAttribute(params object[] args)
        {
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>();
                foreach (object val in args)
                {
                    listOfValues.Add(val);
                }
                listOfValues.Add(lang);
                this.testData.Add(listOfValues.ToArray());
            }
        }

        /// <inheritDoc />
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null) { throw new ArgumentNullException(nameof(testMethod)); }
            if (this.testData.Count == 0) { throw new EmptyException(nameof(testMethod)); }

            UnsupportedLanguagesAttribute unsupportedLangAttr = testMethod.GetCustomAttribute<UnsupportedLanguagesAttribute>();
            if (unsupportedLangAttr != null)
            {
                foreach (string lang in unsupportedLangAttr.UnsuppportedLanguages)
                {
                    this.testData.RemoveAll(l => Array.IndexOf(l, lang) > -1);
                }
            }
            return this.testData;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class UnsupportedLanguagesAttribute : Attribute
    {
        public List<string> UnsuppportedLanguages { get; set; } = new List<string>();
        /// <summary>
        /// Load only supported languages excluding the ones from the provided parameters.
        /// </summary>
        public UnsupportedLanguagesAttribute(params SupportedLanguages[] argsWithUnsupportedLangs)
        {
            foreach (SupportedLanguages s in argsWithUnsupportedLangs)
            {
                this.UnsuppportedLanguages.Add(s.ToString());
            }
        }
    }
    public enum SupportedLanguages
    {
        CSharp
    };
}