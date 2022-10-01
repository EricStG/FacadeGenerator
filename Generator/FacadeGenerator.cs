using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace Generator;

[Generator]
public class FacadeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Adding facade interface
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
           "IFacadeGenerator.g.cs",
           SourceText.From(InterfaceCode, Encoding.UTF8)));

        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
            predicate: FindClassDeclarations,
            transform: GetSemanticTargetForGeneration)
            .Where(static m => m is not null);


        var compilation
          = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilation,
            static (context, source) => Execute(source.Left, source.Right, context));
    }

    static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax?> classDeclarations, SourceProductionContext context)
    {
        if (classDeclarations.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        var distinctDeclarations = classDeclarations.Distinct();

        var classesToGenerate = GetTypesToGenerate(compilation, distinctDeclarations, context.CancellationToken);

        foreach (var classToGenerate in classesToGenerate)
        {
            var (className, body) = GenerateClass(classToGenerate, compilation, context.CancellationToken);
            if (!string.IsNullOrEmpty(body))
            {
                context.AddSource($"{className}.g.cs", SourceText.From(body, Encoding.UTF8));
            }
        }
    }

    private static IEnumerable<(ClassDeclarationSyntax ClassDeclaration, INamedTypeSymbol FacadeInterface)> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax?> classDeclarations, CancellationToken cancellationToken)
    {
        foreach (var classDeclaration in classDeclarations)
        {
            if (classDeclaration == null)
            {
                continue;
            }


            var facadeInterface = GetFacadeInterface(compilation, classDeclaration, cancellationToken);
            if (facadeInterface == null)
            {
                continue;
            }

            yield return (classDeclaration, facadeInterface);
        }

    }

    private static (string ClassName, string Body) GenerateClass((ClassDeclarationSyntax ClassDeclaration, INamedTypeSymbol FacadeInterface) declaration, Compilation compilation, CancellationToken cancellationToken)
    {
        var (classDeclaration, facadeInterface) = declaration;
        var tree = classDeclaration.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(tree);
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken)!;

        var namespaceDeclaration = GetNamespace(classDeclaration);
        if (namespaceDeclaration == null)
        {
            return (string.Empty, string.Empty);
        }

        // We get the generic type argument from the interface. We know it's at [0] because we defined that interface ourselves.
        var interfaceToFacade = facadeInterface.TypeArguments[0];

        if (interfaceToFacade.TypeKind != TypeKind.Interface)
        {
            return (string.Empty, string.Empty);
        }

        // figuring out accessibility to match the partial target class
        string accessibility;
        switch (classSymbol.DeclaredAccessibility)
        {
            case Accessibility.Private:
                accessibility = "private";
                break;
            case Accessibility.ProtectedAndInternal:
                accessibility = "internal protected";
                break;
            case Accessibility.Protected:
                accessibility = "protected";
                break;
            case Accessibility.Internal:
                accessibility = "internal";
                break;
            case Accessibility.Public:
                accessibility = "public";
                break;
            default:
                return (string.Empty, string.Empty);
        }


        var builder = new StringBuilder();

        builder.AppendLine(@$"
#nullable enable
namespace {namespaceDeclaration.Name}
{{
    {accessibility} partial class {classSymbol.Name} : {interfaceToFacade.ToDisplayString()}
    {{
        private partial {interfaceToFacade.ToDisplayString()} GetImplementation();");
        foreach (var member in interfaceToFacade.GetMembers())
        {
            if (member is IMethodSymbol methodSymbol)
            {
                var arguments = GetMethodArguments(methodSymbol);

                builder.Append($"        public {methodSymbol.ReturnType.ToDisplayString()} {methodSymbol.Name}(");
                builder.Append(string.Join(", ", arguments));
                builder.AppendLine(")");
                builder.AppendLine("        {");
                builder.Append("            ");
                if (!methodSymbol.ReturnsVoid)
                {
                    builder.Append("return ");
                }

                builder.Append($"GetImplementation().{methodSymbol.Name}(");
                builder.Append(string.Join(", ", GetMethodArgumentNames(methodSymbol)));
                builder.AppendLine(");");
                builder.AppendLine("        }");
            }

        }

        builder.AppendLine(@"    }
}");


        return (classSymbol.Name, builder.ToString());
    }

    private static IEnumerable<string> GetMethodArguments(IMethodSymbol methodSymbol)
    {
        foreach (var argument in methodSymbol.Parameters)
        {
            yield return $"{argument.Type.ToDisplayString()} {argument.Name}";
        }

    }
    private static IEnumerable<string> GetMethodArgumentNames(IMethodSymbol methodSymbol)
    {
        foreach (var argument in methodSymbol.Parameters)
        {
            yield return argument.Name;
        }

    }

    private static NamespaceDeclarationSyntax? GetNamespace(SyntaxNode syntaxNode)
    {
        return syntaxNode.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
    }

    private static bool FindClassDeclarations (SyntaxNode syntaxNode, CancellationToken _)
    {
        if (syntaxNode is ClassDeclarationSyntax)
        {
            return true;
        }
        return false;
    }

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        var facadeInterface = GetFacadeInterface(context.SemanticModel.Compilation, classDeclaration, cancellationToken);
        if (facadeInterface != null)
        {
            return classDeclaration;
        }
        return null;
    }

    private static INamedTypeSymbol? GetFacadeInterface(Compilation compilation, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
    {
        const string FacadeAttributeName = "FacadeGenerator.IFacadeGenerator<T>";

        var tree = classDeclaration.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(tree);

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);

        if (classSymbol == null)
        {
            return null;
        }


        var implementedInterfaces = classSymbol.AllInterfaces;
        foreach (var implementedInterface in implementedInterfaces)
        {
            if (implementedInterface.OriginalDefinition.ToDisplayString() == FacadeAttributeName)
            {
                return implementedInterface;
            }
        }

        return null;
    }

    private const string InterfaceCode = @"
#nullable enable
namespace FacadeGenerator
{
    internal interface IFacadeGenerator<T> where T: class
    {
    }
}
";
}
