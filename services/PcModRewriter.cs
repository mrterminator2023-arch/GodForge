using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NeoModLoader.services;

/// <summary>
///     Adapts mods written against the PC (Mono) loader to the Il2Cpp game we run on Android.
///     Instead of guessing, it compiles the mod, reads the compiler's own errors and rewrites exactly the
///     expressions the compiler complained about: lambdas that must become Il2Cpp delegates, results that
///     need a Cast, managed types/lists that Unity wants as Il2Cpp types/lists.
/// </summary>
internal static class PcModRewriter
{
    private static readonly Regex ConvertFromTo = new(@"from '([^']+)' to '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex ToType = new(@"to type '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex NonDelegateType = new(@"non-delegate type '([^']+)'", RegexOptions.Compiled);
    private static readonly Regex ConvertTypeToType = new(@"type '([^']+)' to '([^']+)'", RegexOptions.Compiled);

    /// <summary>
    ///     Returns rewritten syntax trees for the errors in <paramref name="pDiagnostics" />, or null when there is
    ///     nothing this pass knows how to fix.
    /// </summary>
    public static List<SyntaxTree> Rewrite(IEnumerable<Diagnostic> pDiagnostics, List<SyntaxTree> pTrees,
                                           CSharpParseOptions pParseOptions, Compilation pCompilation,
                                           out int pFixCount)
    {
        pFixCount = 0;
        var edits = new Dictionary<SyntaxTree, List<(TextSpan span, string text)>>();

        foreach (Diagnostic diagnostic in pDiagnostics)
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error) continue;
            SyntaxTree tree = diagnostic.Location.SourceTree;
            if (tree == null || !pTrees.Contains(tree)) continue;

            (TextSpan span, string text)? edit = BuildEdit(diagnostic, tree, pCompilation);
            if (edit == null) continue;

            if (!edits.TryGetValue(tree, out List<(TextSpan, string)> list))
                edits[tree] = list = new List<(TextSpan, string)>();
            // Overlapping edits (several errors on one expression) would corrupt the source; keep the first.
            if (list.Any(e => e.Item1.OverlapsWith(edit.Value.span))) continue;
            list.Add((edit.Value.span, edit.Value.text));
        }

        if (edits.Count == 0) return null;

        var result = new List<SyntaxTree>(pTrees);
        foreach (KeyValuePair<SyntaxTree, List<(TextSpan span, string text)>> pair in edits)
        {
            string source = pair.Key.GetText().ToString();
            foreach ((TextSpan span, string text) in pair.Value.OrderByDescending(e => e.span.Start))
            {
                source = source.Substring(0, span.Start) + text + source.Substring(span.End);
                pFixCount++;
            }

            int index = result.IndexOf(pair.Key);
            result[index] = CSharpSyntaxTree.ParseText(SourceText.From(source, System.Text.Encoding.UTF8),
                                                       pParseOptions, pair.Key.FilePath);
        }

        return result;
    }

    /// <summary>
    ///     Full name of the parameter the expression is passed as, so the generated code does not depend on the
    ///     using directives of the mod's file.
    /// </summary>
    private static string ParameterTypeOf(SyntaxNode pExpression, Compilation pCompilation)
    {
        return ParameterSymbolOf(pExpression, pCompilation)?.ToDisplayString();
    }

    /// <summary>
    ///     Last resort: the compiler names the delegate type in its message ("to non-delegate type 'UnityAction'");
    ///     look that name up where the error is, which is exactly the scope the mod's own code sees.
    /// </summary>
    private static ITypeSymbol TypeNamedInMessage(string pMessage, SyntaxNode pExpression, Compilation pCompilation)
    {
        Match match = NonDelegateType.Match(pMessage);
        if (!match.Success) return null;

        SemanticModel model = pCompilation.GetSemanticModel(pExpression.SyntaxTree);
        return model.LookupNamespacesAndTypes(pExpression.SpanStart, name: match.Groups[1].Value)
                    .OfType<ITypeSymbol>()
                    .FirstOrDefault(t => t.GetMembers("Invoke").OfType<IMethodSymbol>().Any());
    }

    private static ITypeSymbol ParameterSymbolOf(SyntaxNode pExpression, Compilation pCompilation)
    {
        SemanticModel model = pCompilation.GetSemanticModel(pExpression.SyntaxTree);

        // Works for every position, not just call arguments: property bodies, assignments, initializers.
        if (pExpression is ExpressionSyntax expression_syntax)
        {
            // Il2Cpp delegates are generated as classes deriving from MulticastDelegate, so TypeKind is Class,
            // not Delegate; having an Invoke member is what identifies them.
            ITypeSymbol converted = model.GetTypeInfo(expression_syntax).ConvertedType;
            if (converted != null && converted.GetMembers("Invoke").OfType<IMethodSymbol>().Any()) return converted;
        }

        if (pExpression.Parent is not ArgumentSyntax argument ||
            argument.Parent is not ArgumentListSyntax arguments) return null;

        SymbolInfo info = model.GetSymbolInfo(arguments.Parent!);
        var method = (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) as IMethodSymbol;
        if (method == null) return null;

        int index = arguments.Arguments.IndexOf(argument);
        if (index < 0 || index >= method.Parameters.Length) return null;
        return method.Parameters[index].Type;
    }

    /// <summary>
    ///     The managed delegate matching an Il2Cpp one, so a method group or lambda can be cast before it is
    ///     handed to the game (Il2Cpp delegates cannot be built from either directly).
    /// </summary>
    private static string ManagedDelegateFor(ITypeSymbol pIl2CppDelegate)
    {
        var invoke = pIl2CppDelegate?.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
        if (invoke == null) return null;

        var parameters = invoke.Parameters.Select(p => p.Type.ToDisplayString()).ToList();
        if (invoke.ReturnsVoid)
            return parameters.Count == 0 ? "System.Action" : $"System.Action<{string.Join(", ", parameters)}>";

        parameters.Add(invoke.ReturnType.ToDisplayString());
        return $"System.Func<{string.Join(", ", parameters)}>";
    }

    /// <summary>True when the called method has an overload taking an Il2Cpp array in this argument position.</summary>
    private static bool TakesArrayInstead(SyntaxNode pExpression, Compilation pCompilation)
    {
        if (pExpression.Parent is not ArgumentSyntax argument ||
            argument.Parent is not ArgumentListSyntax arguments) return false;

        SemanticModel model = pCompilation.GetSemanticModel(pExpression.SyntaxTree);
        SymbolInfo info = model.GetSymbolInfo(arguments.Parent!);
        int index = arguments.Arguments.IndexOf(argument);
        if (index < 0) return false;

        IEnumerable<ISymbol> candidates = info.CandidateSymbols;
        if (info.Symbol != null) candidates = candidates.Append(info.Symbol);

        return candidates.OfType<IMethodSymbol>().Any(m => index < m.Parameters.Length &&
                                                           m.Parameters[index].Type.Name.StartsWith("Il2Cpp") &&
                                                           m.Parameters[index].Type.Name.EndsWith("Array"));
    }

    private static (TextSpan span, string text)? BuildEdit(Diagnostic pDiagnostic, SyntaxTree pTree,
                                                           Compilation pCompilation)
    {
        string message = pDiagnostic.GetMessage();
        SyntaxNode root = pTree.GetRoot();

        // The diagnostic often points at a fragment (the "=>" of a lambda, part of a call); rewriting that
        // fragment would break the syntax, so widen it to the whole expression the compiler is talking about.
        SyntaxNode node = root.FindNode(pDiagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        SyntaxNode expression = node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() as SyntaxNode
                                ?? node.FirstAncestorOrSelf<ExpressionSyntax>();
        if (expression == null) return null;
        if (expression.Parent is ArgumentSyntax argument_parent && argument_parent.Expression == expression)
            expression = argument_parent.Expression;

        TextSpan span = expression.Span;
        string original = expression.ToString();

        switch (pDiagnostic.Id)
        {
            // Lambda passed where the game expects an Il2Cpp delegate.
            // A method group or a lambda whose delegate type the compiler cannot infer: both are being passed
            // where the game wants an Il2Cpp delegate.
            case "CS0428":
            case "CS8917":
            case "CS1660":
            case "CS1661":
            {
                ITypeSymbol parameter = ParameterSymbolOf(expression, pCompilation)
                                        ?? TypeNamedInMessage(message, expression, pCompilation);
                string managed = ManagedDelegateFor(parameter);
                if (parameter != null && managed != null)
                    return (span, $"NeoModLoader.AndroidCompatibilityModule.IL2CPPHelper.C<{parameter.ToDisplayString()}>" +
                                  $"(({managed})({original}))");

                Match match = ToType.Match(message);
                if (!match.Success) return null;
                string target = ParameterTypeOf(expression, pCompilation) ?? match.Groups[1].Value;
                return (span, $"NeoModLoader.AndroidCompatibilityModule.IL2CPPHelper.C<{target}>({original})");
            }

            // Il2Cpp APIs return the base Object where the PC loader returned the concrete type.
            case "CS0266":
            {
                Match match = ConvertTypeToType.Match(message);
                if (!match.Success) return null;
                string target = match.Groups[2].Value;
                if (target.StartsWith("Il2Cpp")) return null;

                // Object.Instantiate(SomePrefab.Prefab, parent) is how PC mods spawn our UI prefabs; our own
                // typed factory returns the concrete type, so use it instead of casting the base Object.
                if (expression is InvocationExpressionSyntax invocation &&
                    invocation.Expression.ToString().EndsWith("Instantiate") &&
                    invocation.ArgumentList.Arguments.Count >= 1)
                {
                    var tail = invocation.ArgumentList.Arguments.Skip(1).Select(a => a.ToString());
                    return (span, $"{target}.Instantiate({string.Join(", ", tail)})");
                }

                return (span, $"({original}).Cast<{target}>()");
            }

            // Argument type mismatches: managed Type/List where Il2Cpp variants are required.
            case "CS1503":
            {
                Match match = ConvertFromTo.Match(message);
                if (!match.Success) return null;
                string from = match.Groups[1].Value, to = match.Groups[2].Value;

                if (from == "System.Type" && to == "Il2CppSystem.Type")
                    return (span, $"Il2CppInterop.Runtime.Il2CppType.From({original})");

                // A params Type[] tail has to become a single Il2Cpp array argument.
                if (from == "System.Type" && to.Contains("Il2CppReferenceArray"))
                {
                    var argument = root.FindNode(span).FirstAncestorOrSelf<ArgumentSyntax>();
                    if (argument?.Parent is not ArgumentListSyntax arguments) return null;
                    int first = arguments.Arguments.IndexOf(argument);
                    if (first < 0) return null;
                    var tail = arguments.Arguments.Skip(first).Select(a => a.ToString());
                    TextSpan whole = TextSpan.FromBounds(arguments.Arguments[first].Span.Start,
                                                         arguments.Arguments.Last().Span.End);
                    return (whole, $"NeoModLoader.utils.PcModCompat.T({string.Join(", ", tail)})");
                }

                if (from.StartsWith("System.Collections.Generic.List<"))
                {
                    // Copying element by element across the Il2Cpp boundary is ruinous for mesh-sized lists;
                    // if the API also takes an array, hand it one (that copy is a single block move).
                    if (to.Contains("Il2CppSystem.Collections.Generic.List<") && TakesArrayInstead(expression, pCompilation))
                        return (span, $"NeoModLoader.utils.PcModCompat.A({original})");
                    if (to.Contains("Il2CppSystem.Collections.Generic.List<"))
                        return (span, $"NeoModLoader.utils.PcModCompat.L({original})");
                    if (to.Contains("Il2CppStructArray<") || to.Contains("Il2CppReferenceArray<"))
                        return (span, $"NeoModLoader.utils.PcModCompat.A({original})");
                }

                if (from.EndsWith("UnityAction") && to == "System.Action")
                    return (span, $"NeoModLoader.utils.PcModCompat.Act({original})");

                return null;
            }

            default:
                return null;
        }
    }
}
