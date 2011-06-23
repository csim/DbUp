﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using DbUp.Preprocessors;
using DbUp.ScriptProviders;

namespace DbUp.Execution
{
    /// <summary>
    /// A standard implementation of the IScriptExecutor interface that executes against a SQL Server 
    /// database.
    /// </summary>
    public sealed class SqlScriptExecutor : IScriptExecutor
    {
        private readonly Func<IDbConnection> connectionFactory;
        private readonly ILog log;
        private readonly string schema;
        private readonly IScriptPreprocessor[] additionalScriptPreprocessors;

        ///<summary>
        /// Initializes an instance of the <see cref="SqlScriptExecutor"/> class.
        ///</summary>
        ///<param name="connectionString">The connection string representing the database to act against.</param>
        public SqlScriptExecutor(string connectionString) : this(() => new SqlConnection(connectionString), new ConsoleLog())
        {
        }

        /// <summary>
        /// Initializes an instance of the <see cref="SqlScriptExecutor"/> class.
        /// </summary>
        /// <param name="connectionFactory">The connection factory.</param>
        ///<param name="schema">The schema that contains the table.</param>
        public SqlScriptExecutor(string connectionString, string schema) : this(connectionString, new ConsoleLog(), schema)
        {
        }

        ///<summary>
        /// Initializes an instance of the <see cref="SqlScriptExecutor"/> class.
        ///</summary>
        ///<param name="connectionString">The connection string representing the database to act against.</param>
        /// <param name="log">The logging mechanism.</param>
        public SqlScriptExecutor(Func<IDbConnection> connectionFactory, ILog log)
        public SqlScriptExecutor(string connectionString, ILog log) : this(connectionString, log, "dbo")
        {
        }

        ///<summary>
        /// Initializes an instance of the <see cref="SqlScriptExecutor"/> class.
        ///</summary>
        ///<param name="connectionString">The connection string representing the database to act against.</param>
        ///<param name="log">The logging mechanism.</param>
        ///<param name="schema">The schema that contains the table.</param>
        ///<param name="additionalScriptPreprocessors">Script Preprocessors in addition to variable substitution</param>
        public SqlScriptExecutor(string connectionString, ILog log, string schema, params IScriptPreprocessor[] additionalScriptPreprocessors)
        {
            this.connectionFactory = connectionFactory;
            this.log = log;
            this.schema = schema;
            this.additionalScriptPreprocessors = additionalScriptPreprocessors;
        }

        private static IEnumerable<string> SplitByGoStatements(string script)
        {
            var scriptStatements = Regex.Split(script, "^\\s*GO\\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                                       .Select(x => x.Trim())
                                       .Where(x => x.Length > 0)
                                       .ToArray();
            return scriptStatements;
        }

        /// <summary>
        /// Executes the specified script against a database at a given connection string.
        /// </summary>
        /// <param name="script">The script.</param>
        public void Execute(SqlScript script)
        {
            Execute(script, null);
        }

        /// <summary>
        /// Executes the specified script against a database at a given connection string.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="variables">Variables to replace in the script</param>
        public void Execute(SqlScript script, IDictionary<string, string> variables)
        {
            if (variables == null)
                variables = new Dictionary<string, string>();
            if (!variables.ContainsKey("schema"))
                variables.Add("schema", schema);

            log.WriteInformation("Executing SQL Server script '{0}'", script.Name);

            var contents = new VariableSubstitutionPreprocessor(variables).Process(script.Contents);
            contents = additionalScriptPreprocessors.Aggregate(contents, (current, additionalScriptPreprocessor) => additionalScriptPreprocessor.Process(current));

            var scriptStatements = SplitByGoStatements(contents);
            var index = -1;
            try
            {
                using (var connection = connectionFactory())
                {
                    connection.Open();

                    foreach (var statement in scriptStatements)
                    {
                        index++;
                        var command = connection.CreateCommand();
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException sqlException)
            {
                log.WriteInformation("SQL exception has occured in script: '{0}'", script.Name);
                log.WriteError("Script block number: {0}; Block line {1}; Message: {2}", index, sqlException.LineNumber, sqlException.Procedure, sqlException.Number, sqlException.Message);
                log.WriteError(sqlException.ToString());
                throw;
            }
            catch (DbException sqlException)
            {
                log.WriteInformation("DB exception has occured in script: '{0}'", script.Name);
                log.WriteError("Script block number: {0}; Error code {1}; Message: {2}", index, sqlException.ErrorCode, sqlException.Message);
                log.WriteError(sqlException.ToString());
                throw;
            }
            catch (Exception ex)
            {
                log.WriteInformation("Exception has occured in script: '{0}'", script.Name);
                log.WriteError(ex.ToString());
                throw;
            }
        }
    }
}