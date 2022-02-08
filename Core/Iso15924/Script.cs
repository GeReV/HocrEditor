using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace HocrEditor.Core.Iso15924;

public sealed record Script(string Code, int Number, string Name)
{
    /// <summary>
    /// Gets a read-only collection of all defined ISO-15924 scripts.
    /// </summary>
    public static IReadOnlyList<Script> Database { get; private set; } = new List<Script>();

    public static Script? FromName(string name, bool ignoreCase) => Database.FirstOrDefault(
        script => script.Name.Equals(
            name,
            ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture
        )
    );

    public static Script? FromCode(string code) => Database.FirstOrDefault(
        script => script.Code.Equals(code, StringComparison.InvariantCultureIgnoreCase)
    );

    public static Script? FromNumber(int number) => Database.FirstOrDefault(script => script.Number == number);

    static Script()
    {
        var database = new List<Script>();

        var info = Application.GetResourceStream(new Uri("/Resources/iso15924.txt", UriKind.Relative));

        if (info == null)
        {
            return;
        }

        var reader = new StreamReader(info.Stream);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            var parts = line.Split(';');

            var (code, number, name) = (parts[0], int.Parse(parts[1]), parts[2]);

            database.Add(new Script(code, number, name));
        }

        Database = database.AsReadOnly();
    }
}
