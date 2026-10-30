using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
namespace NutSort.Content { public sealed class OriginalTableSettings { public string Password, Salt, IV; } }
public static class TableAudit
{
    private static string Field(string yaml, string name) { return Regex.Match(yaml, @"(?m)^  " + name + @": (.+)$").Groups[1].Value.Trim(); }
    public static void Main()
    {
        string yaml=File.ReadAllText("Assets/Resources/Configuration/OriginalTables.asset");
        var settings=new NutSort.Content.OriginalTableSettings { Password=Field(yaml,"Password"), Salt=Field(yaml,"Salt"), IV=Field(yaml,"IV") };
        var tables=NutSort.Content.OriginalTableArchive.Read(File.ReadAllBytes("Assets/Resources/tables/tables.bytes"),settings);
        Console.WriteLine(Encoding.UTF8.GetString(tables["level.json"]));
    }
}
