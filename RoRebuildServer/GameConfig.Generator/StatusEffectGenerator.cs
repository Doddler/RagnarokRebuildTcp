using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Tomlyn;

namespace GameConfig.Generator
{
    [Generator(LanguageNames.CSharp)]
    internal class StatusEffectGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            Debug.WriteLine("Execute code generator");
            var myFiles = context.AdditionalTextsProvider.Where(at => at.Path.EndsWith("StatusEffects.toml"))
                .Select((text, cancellationToken) =>
                {
                    var t = text.GetText(cancellationToken);
                    var table = Toml.ToModel(t.ToString());

                    var srcOut = new StringBuilder();
                    srcOut.AppendLine("namespace RebuildSharedData.Enum;");
                    srcOut.AppendLine("public enum CharacterStatusEffect : byte");
                    srcOut.AppendLine("{");
                    srcOut.AppendLine("\tNone,");
                    foreach (var obj in table)
                    {
                        srcOut.AppendLine($"\t{obj.Key},");
                    }

                    srcOut.AppendLine("\tStatusEffectMax,");
                    srcOut.AppendLine("}");
                    return srcOut.ToString();
                });

            context.RegisterSourceOutput(myFiles, (productionContext, text) =>
            {
                productionContext.AddSource($"CharacterStatusEffect.g.cs", SourceText.From(text, Encoding.UTF8));
            });
        }
    }
}
