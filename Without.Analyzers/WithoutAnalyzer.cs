using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Without.Analyzers;

[DiagnosticAnalyzer(LanguageNames.VisualBasic)]
public sealed class WithoutAnalyzer
    : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WITHOUT001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Replace with local variable",
        "Don't use with blocks",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override void Initialize(
        AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeWithBlock, SyntaxKind.WithBlock);
    }

    private void AnalyzeWithBlock(
        SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not WithBlockSyntax withBlockSyntax)
        {
            return;
        }

        var location = withBlockSyntax.GetLocation();

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                location,
                "With block"));
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } = ImmutableArray.Create(Rule);
}