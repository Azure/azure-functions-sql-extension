// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingConstants;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlConverters
    {
        internal class SqlConverter : IConverter<SqlAttribute, SqlCommand>
        {
            private readonly IConfiguration _configuration;
            private readonly ILogger _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlConverter/>"/> class.
            /// </summary>
            /// <param name="configuration"></param>
            /// <param name="logger">ILogger used to log information and warnings</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the configuration is null
            /// </exception>
            public SqlConverter(IConfiguration configuration, ILogger logger)
            {
                this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                this._logger = logger;
                TelemetryInstance.TrackCreate(CreateType.SqlConverter);
            }

            /// <summary>
            /// Creates a SqlCommand containing a SQL connection and the SQL query and parameters specified in attribute.
            /// The user can open the connection in the SqlCommand and use it to read in the results of the query themselves.
            /// </summary>
            /// <param name="attribute">
            /// Contains the SQL query and parameters as well as the information necessary to build the SQL Connection
            /// </param>
            /// <returns>The SqlCommand</returns>
            public SqlCommand Convert(SqlAttribute attribute)
            {
                TelemetryInstance.TrackConvert(ConvertType.SqlCommand);
                this._logger.LogDebugWithThreadId("BEGIN Convert (SqlCommand)");
                var sw = Stopwatch.StartNew();
                try
                {
                    SqlCommand command = SqlBindingUtilities.BuildCommand(attribute, SqlBindingUtilities.BuildConnection(
                                       attribute.ConnectionStringSetting, this._configuration));
                    this._logger.LogDebugWithThreadId($"END Convert (SqlCommand) Duration={sw.ElapsedMilliseconds}ms");
                    return command;
                }
                catch (Exception ex)
                {
                    var props = new Dictionary<TelemetryPropertyName, string>()
                    {
                        { TelemetryPropertyName.Type, ConvertType.SqlCommand.ToString() }
                    };
                    TelemetryInstance.TrackException(TelemetryErrorName.Convert, ex, props);
                    throw;
                }
            }

        }

        /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
        internal class SqlGenericsConverter<T> : IAsyncConverter<SqlAttribute, IEnumerable<T>>, IConverter<SqlAttribute, IAsyncEnumerable<T>>,
            IAsyncConverter<SqlAttribute, string>, IAsyncConverter<SqlAttribute, JArray>
        {
            private readonly IConfiguration _configuration;

            private readonly ILogger _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlGenericsConverter<typeparamref name="T"/>"/> class.
            /// </summary>
            /// <param name="configuration"></param>
            /// <param name="logger">ILogger used to log information and warnings</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the configuration is null
            /// </exception>
            public SqlGenericsConverter(IConfiguration configuration, ILogger logger)
            {
                this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
                this._logger = logger;
                TelemetryInstance.TrackCreate(CreateType.SqlGenericsConverter);
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as a list of POCOs.
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
            /// <returns>An IEnumerable containing the rows read from the user's database in the form of the user-defined POCO</returns>
            public async Task<IEnumerable<T>> ConvertAsync(SqlAttribute attribute, CancellationToken cancellationToken)
            {
                this._logger.LogDebugWithThreadId("BEGIN ConvertAsync (IEnumerable)");
                var sw = Stopwatch.StartNew();
                try
                {
                    string json = await this.BuildItemFromAttributeAsync(attribute, ConvertType.IEnumerable);
                    IEnumerable<T> result = JsonConvert.DeserializeObject<IEnumerable<T>>(json);
                    this._logger.LogDebugWithThreadId($"END ConvertAsync (IEnumerable) Duration={sw.ElapsedMilliseconds}ms");
                    return result;
                }
                catch (Exception ex)
                {
                    var props = new Dictionary<TelemetryPropertyName, string>()
                    {
                        { TelemetryPropertyName.Type, ConvertType.IEnumerable.ToString() }
                    };
                    TelemetryInstance.TrackException(TelemetryErrorName.Convert, ex, props);
                    throw;
                }
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as a JSON-formatted string.
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
            /// <returns>
            /// The JSON string. I.e., if the result has two rows from a table with schema ProductID: int, Name: varchar, Cost: int,
            /// then the returned JSON string could look like
            /// [{"productID":3,"name":"Bottle","cost":90},{"productID":5,"name":"Cup","cost":100}]
            /// </returns>
            async Task<string> IAsyncConverter<SqlAttribute, string>.ConvertAsync(SqlAttribute attribute, CancellationToken cancellationToken)
            {
                this._logger.LogDebugWithThreadId("BEGIN ConvertAsync (Json)");
                var sw = Stopwatch.StartNew();
                try
                {
                    string result = await this.BuildItemFromAttributeAsync(attribute, ConvertType.Json);
                    this._logger.LogDebugWithThreadId($"END ConvertAsync (Json) Duration={sw.ElapsedMilliseconds}ms");
                    return result;
                }
                catch (Exception ex)
                {
                    var props = new Dictionary<TelemetryPropertyName, string>()
                    {
                        { TelemetryPropertyName.Type, ConvertType.Json.ToString() }
                    };
                    TelemetryInstance.TrackException(TelemetryErrorName.Convert, ex, props);
                    throw;
                }
            }

            /// <summary>
            /// Extracts the <see cref="SqlAttribute.ConnectionStringSetting"/> in attribute and uses it to establish a connection
            /// to the SQL database. (Must be virtual for mocking the method in unit tests)
            /// </summary>
            /// <param name="attribute">
            /// The binding attribute that contains the name of the connection string app setting and query.
            /// </param>
            /// <param name="type">
            /// The type of conversion being performed by the input binding.
            /// </param>
            /// <returns></returns>
            public virtual async Task<string> BuildItemFromAttributeAsync(SqlAttribute attribute, ConvertType type)
            {
                using (SqlConnection connection = SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, this._configuration))
                // Ideally, we would like to move away from using SqlDataAdapter both here and in the
                // SqlAsyncCollector since it does not support asynchronous operations.
                using (var adapter = new SqlDataAdapter())
                using (SqlCommand command = SqlBindingUtilities.BuildCommand(attribute, connection))
                {
                    adapter.SelectCommand = command;
                    this._logger.LogDebugWithThreadId("BEGIN OpenBuildItemFromAttributeAsyncConnection");
                    await connection.OpenAsync();
                    this._logger.LogDebugWithThreadId("END OpenBuildItemFromAttributeAsyncConnection");
                    Dictionary<TelemetryPropertyName, string> props = connection.AsConnectionProps();
                    TelemetryInstance.TrackConvert(type, props);
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    this._logger.LogInformation($"{dataTable.Rows.Count} row(s) queried from database: {connection.Database} using Command: {command.CommandText}");
                    // Serialize any DateTime objects in UTC format
                    var jsonSerializerSettings = new JsonSerializerSettings()
                    {
                        DateFormatString = ISO_8061_DATETIME_FORMAT
                    };
                    return JsonConvert.SerializeObject(dataTable, jsonSerializerSettings);
                }

            }

            IAsyncEnumerable<T> IConverter<SqlAttribute, IAsyncEnumerable<T>>.Convert(SqlAttribute attribute)
            {
                try
                {
                    var asyncEnumerable = new SqlAsyncEnumerable<T>(SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, this._configuration), attribute);
                    Dictionary<TelemetryPropertyName, string> props = asyncEnumerable.Connection.AsConnectionProps();
                    TelemetryInstance.TrackConvert(ConvertType.IAsyncEnumerable, props);
                    return asyncEnumerable;
                }
                catch (Exception ex)
                {
                    var props = new Dictionary<TelemetryPropertyName, string>()
                    {
                        { TelemetryPropertyName.Type, ConvertType.IAsyncEnumerable.ToString() }
                    };
                    TelemetryInstance.TrackException(TelemetryErrorName.Convert, ex, props);
                    throw;
                }
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as JArray.
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
            /// <returns>JArray containing the rows read from the user's database in the form of the user-defined POCO</returns>
            async Task<JArray> IAsyncConverter<SqlAttribute, JArray>.ConvertAsync(SqlAttribute attribute, CancellationToken cancellationToken)
            {
                try
                {
                    string json = await this.BuildItemFromAttributeAsync(attribute, ConvertType.JArray);
                    return JArray.Parse(json);
                }
                catch (Exception ex)
                {
                    var props = new Dictionary<TelemetryPropertyName, string>()
                    {
                        { TelemetryPropertyName.Type, ConvertType.JArray.ToString() }
                    };
                    TelemetryInstance.TrackException(TelemetryErrorName.Convert, ex, props);
                    throw;
                }
            }

        }
    }
}