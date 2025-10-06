using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

public class SqlFormatterService
{
    public FormattedSqlResult FormatSql(string query, string sqlVersion = "160")
    {
        try
        {
            var parser = GetParser(sqlVersion);
            
            using var reader = new StringReader(query);
            var fragment = parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                return new FormattedSqlResult
                {
                    Success = false,
                    Errors = errors.Select(e => new SqlError
                    {
                        Line = e.Line,
                        Column = e.Column,
                        Message = e.Message
                    }).ToList()
                };
            }

            // Gera SQL formatado
            var generator = GetScriptGenerator(sqlVersion);
            generator.GenerateScript(fragment, out var formattedSql);

            return new FormattedSqlResult
            {
                Success = true,
                OriginalSql = query,
                FormattedSql = formattedSql,
                Stats = new SqlStats
                {
                    OriginalLines = query.Split('\n').Length,
                    FormattedLines = formattedSql.Split('\n').Length,
                    OriginalLength = query.Length,
                    FormattedLength = formattedSql.Length
                }
            };
        }
        catch (Exception ex)
        {
            return new FormattedSqlResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private TSqlParser GetParser(string sqlVersion)
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

    private SqlScriptGenerator GetScriptGenerator(string sqlVersion)
    {
        return sqlVersion switch
        {
            "90" => new Sql90ScriptGenerator(),
            "100" => new Sql100ScriptGenerator(),
            "110" => new Sql110ScriptGenerator(),
            "120" => new Sql120ScriptGenerator(),
            "130" => new Sql130ScriptGenerator(),
            "140" => new Sql140ScriptGenerator(),
            "150" => new Sql150ScriptGenerator(),
            "160" => new Sql160ScriptGenerator(),
            _ => new Sql160ScriptGenerator()
        };
    }
}

public class FormattedSqlResult
{
    public bool Success { get; set; }
    public string? OriginalSql { get; set; }
    public string? FormattedSql { get; set; }
    public SqlStats? Stats { get; set; }
    public List<SqlError>? Errors { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SqlStats
{
    public int OriginalLines { get; set; }
    public int FormattedLines { get; set; }
    public int OriginalLength { get; set; }
    public int FormattedLength { get; set; }
}

public class SqlError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";
}
