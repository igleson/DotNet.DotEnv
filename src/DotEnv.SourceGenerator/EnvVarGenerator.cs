using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotEnv.SourceGenerator;

[Generator]
public class EnvVarGenerator : IIncrementalGenerator
{
    private const char EqualsChar = '=';
    private const string Underscore = "_";
    private const string CommentLine = "#";
    private static readonly string[] SupportedPropertiesTypes = ["string", "bool", "decimal", "double", "float", "int", "long"];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dotEnvClassProvider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: (node, _) => IsTargetForGeneration(node),
            transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node);

        var dotEnvFileProvider = context.AdditionalTextsProvider.Where(fileProvider => fileProvider.Path.EndsWith(".env"));

        var valuesProvider = dotEnvFileProvider.Collect().Combine(dotEnvClassProvider.Collect());

        context.RegisterSourceOutput(valuesProvider, (sourceProductionContext, node)
            => Execute(node, sourceProductionContext));
    }

    private static bool IsTargetForGeneration(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDeclaration
               && classDeclaration.AttributeLists
                   .SelectMany(attrList => attrList.Attributes)
                   .Any(attr => attr.Name.ToString().Equals("DotEnvAutoGenerated"));
    }

    private static void Execute(
        (ImmutableArray<AdditionalText> DotEnvFileNode, ImmutableArray<ClassDeclarationSyntax> DotEnvClassNode) node,
        SourceProductionContext context)
    {
        if (node.DotEnvFileNode.IsEmpty) return;
        if (node.DotEnvClassNode.IsEmpty) return;

        var fileTexts = node.DotEnvFileNode
            .Select(additionalText => additionalText.GetText()?.ToString())
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

        var dict = new Dictionary<string, string>();

        foreach (var line in fileTexts.Select(fileText => fileText.Split('\n')).SelectMany(lines => lines))
        {
            if (line.StartsWith(CommentLine)) continue;
            try
            {
                var l = line.Replace("\r", string.Empty);
                var splitted = l.Split(EqualsChar);
                var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(splitted[0].ToLowerInvariant()).Replace(Underscore, string.Empty);

                var value = splitted[1];
                dict.Add(name, value);
            }
            catch
            {
                // ignored
            }
        }

        foreach (var classDeclarationSyntax in node.DotEnvClassNode)
        {
            var propertiesDeclarations = classDeclarationSyntax
                .ChildNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)))
                .Where(property => property.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)))
                .Where(PropertyOnlyHasGetAccessor)
                .Where(prop => dict.ContainsKey(prop.Identifier.ToString()))
                .ToList();

            var propertiesCode =
                string.Join("\n\t",
                    propertiesDeclarations
                        .Select(prop =>
                        {
                            var propValue = GenerateValueFor(prop, dict[prop.Identifier.ToString()]);
                            return propValue is null ? null : $"public static partial {prop.Type} {prop.Identifier} => {propValue};";
                        })
                        .Where(p => p is not null));

            var @namespace = GetNamespace(classDeclarationSyntax);

            context.AddSource($"{classDeclarationSyntax.Identifier}.g",
                $$"""
                  namespace {{@namespace}};

                  public static partial class {{classDeclarationSyntax.Identifier}}
                  {
                      {{propertiesCode}}
                  }
                  """);
        }
    }

    private static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        var nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        var potentialNamespaceParent = syntax.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();

            // Keep moving "out" of the namespace declarations until we 
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }

        // return the final namespace
        return nameSpace;
    }

    private static bool PropertyOnlyHasGetAccessor(PropertyDeclarationSyntax property)
    {
        var propType = property.Type.ToString().ToLower();
        return SupportedPropertiesTypes.Contains(propType) && property.AccessorList?.Accessors.Count == 1
                                                           && property.AccessorList.Accessors.First().Kind() == SyntaxKind.GetAccessorDeclaration;
    }

    private static string? GenerateValueFor(PropertyDeclarationSyntax property, string stringfiedValue)
    {
        return property.Type.ToString().ToLower() switch
        {
            "string" => $"\"{stringfiedValue}\"",
            "bool" => bool.TryParse(stringfiedValue, out _) ? stringfiedValue : null,
            "decimal" => decimal.TryParse(stringfiedValue, out var m) ? $"{m}m" : null,
            "double" => double.TryParse(stringfiedValue, out var d) ? $"{d}d" : null,
            "float" => float.TryParse(stringfiedValue, out var f) ? $"{f}f" : null,
            "int" => int.TryParse(stringfiedValue, out _) ? stringfiedValue : null,
            "long" => long.TryParse(stringfiedValue, out var l) ? $"{l}L" : null,
            _ => null
        };
    }
}