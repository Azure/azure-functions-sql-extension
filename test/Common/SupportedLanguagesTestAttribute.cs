// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using Xunit.Sdk;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <summary>
    /// SqlInlineData attribute class for adding language parameter data to tests. This allows
    /// tests to be run against all the supported languages list that are specified in the SupportedLanguages
    /// list of this class (ex: CSharp, JavaScript)
    /// </summary>
    public class SqlInlineDataAttribute : DataAttribute
    {
        private readonly Dictionary<string, List<List<object>>> testData = new Dictionary<string, List<List<object>>>();
        private readonly string _propertyName = "data";

        /// <summary>
        /// Load supported languages as test parameters data for the test.
        /// </summary>
        public SqlInlineDataAttribute()
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>(1) { lang };
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);

        }
        /// <summary>
        /// Load the test parameters data for all supported languages for inline use of the test.
        /// </summary>
        /// <param name="args">The test parameters to insert inline for the test</param>
        public SqlInlineDataAttribute(params object[] args)
        {
            var langData = new List<List<object>>();
            foreach (string lang in Enum.GetNames(typeof(SupportedLanguages)))
            {
                var listOfValues = new List<object>();
                foreach (object val in args)
                {
                    listOfValues.Add(val);
                }
                listOfValues.Add(lang);
                langData.Add(listOfValues);
            }
            this.testData.Add(this._propertyName, langData);
        }

        /// <inheritDoc />
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null) { throw new ArgumentNullException(nameof(testMethod)); }
            if (this.testData.Count == 0) { throw new EmptyException(nameof(testMethod)); }

            UnsupportedLanguagesAttribute unsupportedLangAttr = testMethod.GetCustomAttribute<UnsupportedLanguagesAttribute>();
            if (unsupportedLangAttr != null)
            {
                var unsupportedLanguages = new List<string>();
                unsupportedLanguages.AddRange(unsupportedLangAttr.UnsuppportedLanguages);
                foreach (string lang in unsupportedLanguages)
                {
                    List<List<object>> dataList = this.testData.GetValueOrDefault(this._propertyName);
                    dataList.RemoveAll(l => l.Contains(lang));
                    this.testData[this._propertyName] = dataList;
                }
            }

            string serializedData = JsonConvert.SerializeObject(this.testData);
            var allData = JObject.Parse(serializedData);
            JToken data = allData[this._propertyName];
            return data.ToObject<List<object[]>>();
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

    [Flags]
    public enum SupportedLanguages
    {
        CSharp
    };
}