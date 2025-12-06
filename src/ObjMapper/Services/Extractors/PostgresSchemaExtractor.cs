using System.Data.Common;
using Npgsql;
using ObjMapper.Models;

namespace ObjMapper.Services.Extractors;

/// <summary>
/// Extracts schema from PostgreSQL databases.
/// </summary>
public class PostgresSchemaExtractor : BaseSchemaExtractor
{
    protected override DatabaseType DatabaseType => DatabaseType.PostgreSql;
    protected override string DefaultSchemaName => "public";

    protected override DbConnection CreateConnection(string connectionString) => 
        new NpgsqlConnection(connectionString);

    protected override async Task<List<(string name, string schema)>> GetTablesAsync(DbConnection connection, string schemaName)
    {
        var tables = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT table_name, table_schema 
            FROM information_schema.tables 
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name";
        AddParameter(command, "schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return tables;
    }

    protected override async Task<List<(string name, string schema)>> GetViewsAsync(DbConnection connection, string schemaName)
    {
        var views = new List<(string name, string schema)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT table_name, table_schema 
            FROM information_schema.views 
            WHERE table_schema = @schema
            ORDER BY table_name";
        AddParameter(command, "schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add((reader.GetString(0), reader.GetString(1)));
        }
        
        return views;
    }

    protected override async Task<List<ColumnInfo>> GetColumnsAsync(DbConnection connection, string schemaName, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                column_name, 
                data_type, 
                is_nullable,
                COALESCE(col_description((table_schema || '.' || table_name)::regclass, ordinal_position), '') as comment
            FROM information_schema.columns 
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position";
        AddParameter(command, "schema", schemaName);
        AddParameter(command, "table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Schema = schemaName,
                Table = tableName,
                Column = reader.GetString(0),
                Type = reader.GetString(1),
                Nullable = reader.GetString(2) == "YES",
                Comment = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }
        
        return columns;
    }

    protected override async Task<List<IndexInfo>> GetIndexesAsync(DbConnection connection, string schemaName, string tableName)
    {
        var indexes = new List<IndexInfo>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                i.relname as index_name,
                array_agg(a.attname ORDER BY k.n) as column_names,
                ix.indisunique as is_unique
            FROM pg_index ix
            JOIN pg_class i ON ix.indexrelid = i.oid
            JOIN pg_class t ON ix.indrelid = t.oid
            JOIN pg_namespace n ON t.relnamespace = n.oid
            JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY AS k(attnum, n) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE n.nspname = @schema 
              AND t.relname = @table
              AND NOT ix.indisprimary
            GROUP BY i.relname, ix.indisunique";
        AddParameter(command, "schema", schemaName);
        AddParameter(command, "table", tableName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnNames = (string[])reader.GetValue(1);
            indexes.Add(new IndexInfo
            {
                Schema = schemaName,
                Table = tableName,
                Name = reader.GetString(0),
                Key = string.Join(",", columnNames),
                Type = reader.GetBoolean(2) ? "unique" : "btree"
            });
        }
        
        return indexes;
    }

    protected override async Task<List<RelationshipInfo>> GetRelationshipsAsync(DbConnection connection, string schemaName)
    {
        var fkGroups = new Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                tc.constraint_name,
                tc.table_schema,
                tc.table_name,
                kcu.column_name,
                ccu.table_schema AS foreign_table_schema,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name
              AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name
              AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @schema
            ORDER BY tc.constraint_name, kcu.ordinal_position";
        AddParameter(command, "schema", schemaName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ProcessForeignKeyRow(reader, fkGroups);
        }
        
        return ProcessForeignKeyGroups(fkGroups);
    }

    protected override async Task<List<ScalarFunctionInfo>> GetScalarFunctionsAsync(DbConnection connection, string schemaName)
    {
        var functions = new List<ScalarFunctionInfo>();
        var functionList = new List<(string schema, string name, string returnType)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                n.nspname AS schema_name,
                p.proname AS function_name,
                pg_get_function_result(p.oid) AS return_type
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname = @schema
              AND p.prokind = 'f'
              AND p.proretset = false
              AND NOT EXISTS (SELECT 1 FROM pg_aggregate WHERE aggfnoid = p.oid)
            ORDER BY p.proname";
        AddParameter(command, "schema", schemaName);
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                functionList.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }
        
        foreach (var (funcSchema, funcName, returnType) in functionList)
        {
            var functionInfo = new ScalarFunctionInfo
            {
                Schema = funcSchema,
                Name = funcName,
                ReturnType = returnType,
                Parameters = await GetFunctionParametersAsync(connection, funcSchema, funcName)
            };
            functions.Add(functionInfo);
        }
        
        return functions;
    }

    private async Task<List<ScalarFunctionParameter>> GetFunctionParametersAsync(DbConnection connection, string schemaName, string functionName)
    {
        var parameters = new List<ScalarFunctionParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COALESCE(p.parameter_name, 'p' || p.ordinal_position::text) AS param_name,
                p.data_type,
                p.ordinal_position
            FROM information_schema.parameters p
            JOIN information_schema.routines r ON p.specific_name = r.specific_name
            WHERE r.routine_schema = @schema 
              AND r.routine_name = @function
              AND p.parameter_mode IN ('IN', 'INOUT')
            ORDER BY p.ordinal_position";
        AddParameter(command, "schema", schemaName);
        AddParameter(command, "function", functionName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            parameters.Add(new ScalarFunctionParameter
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2)
            });
        }
        
        return parameters;
    }

    protected override async Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(DbConnection connection, string schemaName)
    {
        var procedures = new List<StoredProcedureInfo>();
        var procList = new List<(string schema, string name, string specificName)>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                r.routine_schema,
                r.routine_name,
                r.specific_name
            FROM information_schema.routines r
            WHERE r.routine_schema = @schema
              AND r.routine_type = 'PROCEDURE'
            ORDER BY r.routine_name";
        AddParameter(command, "schema", schemaName);
        
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                procList.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }
        
        foreach (var (procSchema, procName, specificName) in procList)
        {
            var procInfo = new StoredProcedureInfo
            {
                Schema = procSchema,
                Name = procName,
                Parameters = await GetProcedureParametersAsync(connection, procSchema, specificName)
            };
            
            // Determine output type based on OUT parameters and procedure definition
            procInfo.OutputType = DetermineProcedureOutputType(procInfo.Parameters);
            
            procedures.Add(procInfo);
        }
        
        return procedures;
    }

    private async Task<List<StoredProcedureParameter>> GetProcedureParametersAsync(DbConnection connection, string schemaName, string specificName)
    {
        var parameters = new List<StoredProcedureParameter>();
        
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COALESCE(p.parameter_name, 'p' || p.ordinal_position::text) AS param_name,
                p.data_type,
                p.ordinal_position,
                p.parameter_mode
            FROM information_schema.parameters p
            WHERE p.specific_schema = @schema 
              AND p.specific_name = @specificName
            ORDER BY p.ordinal_position";
        AddParameter(command, "schema", schemaName);
        AddParameter(command, "specificName", specificName);
        
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var mode = reader.GetString(3);
            parameters.Add(new StoredProcedureParameter
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2),
                IsOutput = mode == "OUT" || mode == "INOUT"
            });
        }
        
        return parameters;
    }

    private static StoredProcedureOutputType DetermineProcedureOutputType(List<StoredProcedureParameter> parameters)
    {
        var outputParams = parameters.Count(p => p.IsOutput);
        return outputParams switch
        {
            0 => StoredProcedureOutputType.None,
            1 => StoredProcedureOutputType.Scalar,
            _ => StoredProcedureOutputType.Tabular
        };
    }

    private static void ProcessForeignKeyRow(DbDataReader reader,
        Dictionary<string, (RelationshipInfo rel, List<string> keys, List<string> foreigns)> fkGroups)
    {
        var constraintName = reader.GetString(0);
        var tableSchema = reader.GetString(1);
        var tableName = reader.GetString(2);
        var columnName = reader.GetString(3);
        var foreignSchema = reader.GetString(4);
        var foreignTable = reader.GetString(5);
        var foreignColumn = reader.GetString(6);
        
        if (!fkGroups.TryGetValue(constraintName, out var group))
        {
            var rel = new RelationshipInfo
            {
                Name = constraintName,
                SchemaFrom = tableSchema,
                SchemaTo = foreignSchema,
                TableFrom = tableName,
                TableTo = foreignTable
            };
            group = (rel, [], []);
            fkGroups[constraintName] = group;
        }
        
        group.keys.Add(foreignColumn);
        group.foreigns.Add(columnName);
    }
}
