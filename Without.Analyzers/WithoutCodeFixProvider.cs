using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Without.Analyzers;

[ExportCodeFixProvider(LanguageNames.VisualBasic), Shared]
public sealed class WithoutCodeFixProvider
    : CodeFixProvider
{
    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        [WithoutAnalyzer.DiagnosticId];

    public override async Task RegisterCodeFixesAsync(
        CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.Single();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnosticNode = root?.FindNode(diagnosticSpan);

        if (diagnosticNode is not WithBlockSyntax withBlockSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with block with local variable",
                c => ReplaceBlock(
                    context.Document,
                    withBlockSyntax,
                    c),
                equivalenceKey: "WITHOUT001"),
            diagnostic);
    }

    private async Task<Document> ReplaceBlock(
        Document document,
        WithBlockSyntax withBlockSyntax,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is null)
        {
            return document;
        }

        var withExpression = withBlockSyntax.WithStatement.Expression;
        var expressionType = semanticModel.GetTypeInfo(withExpression);

        var typeString = (expressionType.ConvertedType ?? expressionType.Type)
            .ToDisplayString(
                format: SymbolDisplayFormat.VisualBasicShortErrorMessageFormat);
        var localName =
            $"__with{typeString}{Guid.NewGuid().ToString().Substring(0, 4)}";

        var declarator = SyntaxFactory.VariableDeclarator(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.ModifiedIdentifier(localName)),
            null,
            SyntaxFactory.EqualsValue(withExpression));
        var dimStatement = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.DimKeyword)),
            SyntaxFactory.SeparatedList([declarator]));
        var rewriter = new WithBodyRewriter(localName);
        var newStatements = withBlockSyntax.Statements
            .Select(s => (StatementSyntax)rewriter.Visit(s))
            .ToList();

        var newStatementList =
            SyntaxFactory.List<StatementSyntax>().Add(dimStatement)
                .AddRange(newStatements);

        var newRoot = root.ReplaceNode(
            withBlockSyntax,
            newStatementList);
        return document.WithSyntaxRoot(newRoot);
    }

    private sealed class WithBodyRewriter(
        string localName)
        : VisualBasicSyntaxRewriter
    {
        public override SyntaxNode VisitWithBlock(
            WithBlockSyntax node)
        {
            // Nested with: Only visit the expression, not the nested statements
            var expr = Visit(node.WithStatement.Expression);
            if (expr == node.WithStatement.Expression
                || expr is not ExpressionSyntax e)
            {
                return node;
            }

            return node.WithWithStatement(
                node.WithStatement.WithExpression(e));
        }

        public override SyntaxNode VisitMemberAccessExpression(
            MemberAccessExpressionSyntax node)
        {
            if (node.Expression is not null)
            {
                return base.VisitMemberAccessExpression(node);
            }

            var name = node.Name;
            var newExpr = SyntaxFactory.IdentifierName(localName);
            var result = SyntaxFactory.SimpleMemberAccessExpression(
                newExpr,
                node.OperatorToken,
                name);
            return base.VisitMemberAccessExpression(result);
        }
    }
}