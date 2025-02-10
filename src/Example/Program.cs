using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var tree = CSharpSyntaxTree.ParseText(@"
 public static partial class EnvVars
{
    public static  string SqlConnectionString { get; }
}");

var compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees: [tree]);

var model = compilation.GetSemanticModel(tree);

var prop = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().Single();
