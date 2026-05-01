using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Tomlyn;

//var entry in GetCsvRows<CsvItemWeapon>("ItemsWeapons.csv")

namespace GameConfig.Generator
{
    [Generator(LanguageNames.CSharp)]
    internal class WeaponClassGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            Debug.WriteLine("Execute WeaponClass code generator");

            var myFiles = context.AdditionalTextsProvider.Where(at => at.Path.EndsWith("WeaponClass.csv"))
                .Select((text, cancellationToken) =>
                {
                    var t = text.GetText(cancellationToken);

                    using (var tr = new StringReader(t.ToString()) as TextReader)
                    {
                        var srcOut = new StringBuilder();

                        srcOut.AppendLine("namespace RebuildSharedData.Enum;");
                        srcOut.AppendLine("public enum WeaponClass : byte");
                        srcOut.AppendLine("{");
                        srcOut.AppendLine("\tNone,");

                        string line;
                        while ((line = tr.ReadLine()) != null)
                        {
                            var s = line.Split(',');
                            if (s.Length > 2 && int.TryParse(s[0], out var id) && !string.IsNullOrWhiteSpace(s[1]))
                            {
                                if (s[1].StartsWith("2H"))
                                    srcOut.AppendLine($"\tTwoHand{s[1].Substring(2)} = {id},");
                                else
                                    srcOut.AppendLine($"\t{s[1]} = {id},");

                            }
                        }

                        srcOut.AppendLine("}");
                        return srcOut.ToString();
                    }
                });

            context.RegisterSourceOutput(myFiles, (productionContext, text) =>
            {
                productionContext.AddSource($"WeaponClass.g.cs", SourceText.From(text, Encoding.UTF8));
            });
        }
    }
}