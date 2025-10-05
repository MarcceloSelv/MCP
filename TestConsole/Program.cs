using Microsoft.SqlServer.TransactSql.ScriptDom;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== SQL Validator Test Console ===\n");

        // Testes de sintaxe válida
        Console.WriteLine("--- TESTE 1: SELECT válido ---");
        TestQuery("SELECT * FROM Users WHERE Id = 1");

        Console.WriteLine("\n--- TESTE 2: SELECT com erro de sintaxe (FORM ao invés de FROM) ---");
        TestQuery("SELECT * FORM Users WHERE Id = 1");

        Console.WriteLine("\n--- TESTE 3: STRING_AGG (SQL 2017+) no parser SQL 2016 ---");
        TestQueryWithVersion("SELECT CategoryId, STRING_AGG(ProductName, ', ') FROM Products GROUP BY CategoryId", "130");

        Console.WriteLine("\n--- TESTE 4: STRING_AGG (SQL 2017+) no parser SQL 2017 ---");
        TestQueryWithVersion("SELECT CategoryId, STRING_AGG(ProductName, ', ') FROM Products GROUP BY CategoryId", "140");

        Console.WriteLine("\n--- TESTE 5: Função que NÃO EXISTE (mas sintaxe válida) ---");
        TestQuery("SELECT FUNCAO_INEXISTENTE(campo1, campo2) FROM Tabela");

        Console.WriteLine("\n--- TESTE 6: Tabela que não existe (mas sintaxe válida) ---");
        TestQuery("SELECT * FROM TabelaQueNaoExiste WHERE campo = 1");

        Console.WriteLine("\n--- TESTE 7: Coluna inválida em sintaxe (faltando vírgula) ---");
        TestQuery("SELECT col1 col2 FROM Users");

        Console.WriteLine("\n--- TESTE 8: CREATE TABLE ---");
        TestQuery(@"CREATE TABLE Customers (
            Id INT PRIMARY KEY IDENTITY(1,1),
            Name NVARCHAR(100) NOT NULL,
            Email NVARCHAR(255)
        )");

        Console.WriteLine("\n--- TESTE 9: Extended Events ---");
        TestQuery(@"CREATE EVENT SESSION [QueryPerformance] ON SERVER 
            ADD EVENT sqlserver.sql_statement_completed
            ADD TARGET package0.event_file(SET filename=N'C:\Temp\QueryPerf.xel')");

        Console.WriteLine("\n--- TESTE 10: DBCC Commands ---");
        TestQuery("DBCC USEROPTIONS");

        Console.WriteLine("\n--- TESTE 11: XML methods ---");
        TestQuery(@"DECLARE @xml XML = '<root><item>test</item></root>';
            SELECT @xml.value('(/root/item)[1]', 'varchar(50)')");

        Console.WriteLine("\n--- TESTE 12: Múltiplos statements ---");
        TestQueryWithDetails(@"
            SELECT * FROM Users;
            UPDATE Users SET Active = 1;
            DELETE FROM TempTable;
        ");

        Console.WriteLine("\n\n=== CONCLUSÃO ===");
        Console.WriteLine("O parser valida SINTAXE, não SEMÂNTICA.");
        Console.WriteLine("- ✓ Detecta erros de SINTAXE (palavras-chave erradas, vírgulas faltando, etc)");
        Console.WriteLine("- ✗ NÃO valida se funções existem");
        Console.WriteLine("- ✗ NÃO valida se tabelas existem");
        Console.WriteLine("- ✗ NÃO valida se colunas existem");
        Console.WriteLine("- ✗ NÃO valida tipos de dados compatíveis");
        Console.WriteLine("\nPara validação SEMÂNTICA, você precisa de conexão com o banco de dados!");

        Console.WriteLine("\nPressione qualquer tecla para sair...");
        Console.ReadKey();
    }

    static void TestQuery(string query)
    {
        TestQueryWithVersion(query, "160"); // SQL Server 2022 por padrão
    }

    static void TestQueryWithVersion(string query, string sqlVersion)
    {
        var parser = GetParser(sqlVersion);
        var versionName = GetVersionName(sqlVersion);

        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        Console.WriteLine($"Query: {query.Substring(0, Math.Min(80, query.Length))}...");
        Console.WriteLine($"Versão: {versionName}");
        
        if (errors.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Sintaxe VÁLIDA");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {errors.Count} erro(s) de sintaxe:");
            Console.ResetColor();
            
            foreach (var error in errors)
            {
                Console.WriteLine($"  - Linha {error.Line}, Coluna {error.Column}: {error.Message}");
            }
        }
    }

    static void TestQueryWithDetails(string query)
    {
        var parser = new TSql160Parser(true);

        using var reader = new StringReader(query);
        var fragment = parser.Parse(reader, out var errors);

        Console.WriteLine($"Query: {query.Trim().Substring(0, Math.Min(60, query.Trim().Length))}...");
        
        if (errors.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Sintaxe VÁLIDA");
            Console.ResetColor();

            // Análise detalhada do AST
            var visitor = new SqlAnalysisVisitor();
            fragment.Accept(visitor);

            Console.WriteLine($"Detalhes do AST:");
            Console.WriteLine($"  - Statements: {visitor.StatementCount}");
            Console.WriteLine($"  - Tabelas referenciadas: {visitor.TableCount}");
            Console.WriteLine($"  - SELECTs: {visitor.SelectCount}");
            Console.WriteLine($"  - INSERTs: {visitor.InsertCount}");
            Console.WriteLine($"  - UPDATEs: {visitor.UpdateCount}");
            Console.WriteLine($"  - DELETEs: {visitor.DeleteCount}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {errors.Count} erro(s) de sintaxe:");
            Console.ResetColor();
            
            foreach (var error in errors)
            {
                Console.WriteLine($"  - Linha {error.Line}, Coluna {error.Column}: {error.Message}");
            }
        }
    }

    static TSqlParser GetParser(string sqlVersion)
    {
        return sqlVersion switch
        {
            "90" => new TSql90Parser(true),
            "100" => new TSql100Parser(true),
            "110" => new TSql110Parser(true),
            "120" => new TSql120Parser(true),
            "130" => new TSql130Parser(true),
            "140" => new TSql140Parser(true),
            "150" => new TSql150Parser(true),
            "160" => new TSql160Parser(true),
            _ => new TSql160Parser(true)
        };
    }

    static string GetVersionName(string version)
    {
        return version switch
        {
            "90" => "SQL Server 2005",
            "100" => "SQL Server 2008",
            "110" => "SQL Server 2012",
            "120" => "SQL Server 2014",
            "130" => "SQL Server 2016",
            "140" => "SQL Server 2017",
            "150" => "SQL Server 2019",
            "160" => "SQL Server 2022",
            _ => "SQL Server 2022"
        };
    }
}

// Visitor para análise detalhada do AST
public class SqlAnalysisVisitor : TSqlFragmentVisitor
{
    public int StatementCount { get; private set; }
    public int TableCount { get; private set; }
    public int SelectCount { get; private set; }
    public int InsertCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int DeleteCount { get; private set; }

    public override void Visit(TSqlStatement node)
    {
        StatementCount++;
        base.Visit(node);
    }

    public override void Visit(NamedTableReference node)
    {
        TableCount++;
        base.Visit(node);
    }

    public override void Visit(SelectStatement node)
    {
        SelectCount++;
        base.Visit(node);
    }

    public override void Visit(InsertStatement node)
    {
        InsertCount++;
        base.Visit(node);
    }

    public override void Visit(UpdateStatement node)
    {
        UpdateCount++;
        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        DeleteCount++;
        base.Visit(node);
    }
}
