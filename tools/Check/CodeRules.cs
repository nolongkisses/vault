using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace VaultCheck;

// Design scales shared by C# and XAML: raw values outside them fail the check (docs/conventions.md).
static class Scales
{
    internal static readonly double[] FontSizes = [11, 12, 13, 15, 17, 26];
    internal static readonly double[] Spacing = [0, 2, 4, 6, 8, 12, 16, 24, 32, 40];
    internal static readonly double[] Radii = [0, 2, 4, 6, 10, 12, 16];
    internal static readonly Regex HexColor = new("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
    internal static bool InTheme(string relative) => relative.StartsWith("NoxVault/Interface/Theme/", StringComparison.Ordinal);
    internal static bool InTests(string relative) => relative.StartsWith("NoxVault/Tests/", StringComparison.Ordinal);
}

// Function size, parameter count, null-forgiving, swallowed exceptions and raw design values in C#.
static class CodeRules
{
    const int MaxFunctionLines = 60;
    const int MaxParameters = 5;

    internal static IEnumerable<string> Check(string relative, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var violations = new List<string>();
        foreach (var node in root.DescendantNodes())
        {
            string? problem = node switch
            {
                BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax => FunctionSize(node),
                _ => null,
            };
            problem ??= Parameters(node) ?? Literal(node, relative) ?? NullForgiving(node, relative) ?? Catch(node);
            if (problem != null) violations.Add($"{relative}:{Line(node)}: {problem}");
        }
        return violations;
    }

    static string? FunctionSize(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        int lines = span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        return lines > MaxFunctionLines ? $"function {Name(node)} has {lines} lines, limit {MaxFunctionLines}" : null;
    }

    static string? Parameters(SyntaxNode node)
    {
        var list = node switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList,
            LocalFunctionStatementSyntax local => local.ParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList,
            TypeDeclarationSyntax type => type.ParameterList,
            _ => null,
        };
        int count = list?.Parameters.Count ?? 0;
        return count > MaxParameters ? $"{Name(node)} takes {count} parameters, limit {MaxParameters}" : null;
    }

    static string? Literal(SyntaxNode node, string relative)
    {
        if (Scales.InTests(relative)) return null;
        if (node is LiteralExpressionSyntax { Token.Value: string text } && Scales.HexColor.IsMatch(text) && !Scales.InTheme(relative))
            return $"colour {text} outside Interface/Theme";
        if (node is AssignmentExpressionSyntax font && Target(font.Left) == "FontSize"
            && Number(font.Right) is { } size && !Scales.FontSizes.Contains(size))
            return $"font size {size} outside the type scale";
        if (node is ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax type } creation && creation.ArgumentList != null)
        {
            var scale = type.Identifier.Text switch { "Thickness" => Scales.Spacing, "CornerRadius" => Scales.Radii, _ => null };
            var off = scale == null ? null : creation.ArgumentList.Arguments.Select(a => Number(a.Expression))
                .FirstOrDefault(v => v is { } value && !scale.Contains(Math.Abs(value)));
            if (off != null) return $"{type.Identifier.Text} value {off} outside the scale";
        }
        return null;
    }

    static string? NullForgiving(SyntaxNode node, string relative) =>
        node.IsKind(SyntaxKind.SuppressNullableWarningExpression) && !Scales.InTests(relative) ? "null-forgiving operator" : null;

    static string? Catch(SyntaxNode node)
    {
        if (node is not CatchClauseSyntax clause || clause.Filter != null) return null;
        var type = clause.Declaration?.Type.ToString();
        if (type is not (null or "Exception" or "System.Exception")) return null;
        bool rethrows = clause.Block.DescendantNodes().Any(n => n is ThrowStatementSyntax or ThrowExpressionSyntax);
        bool explained = clause.DescendantTrivia().Concat(clause.GetLeadingTrivia()).Concat(clause.GetTrailingTrivia())
            .Any(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia));
        return rethrows || explained ? null : "catch-all without rethrow or a comment saying why";
    }

    static string? Target(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax name => name.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => null,
    };

    static double? Number(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal when literal.Token.Value is IConvertible value and not string => value.ToDouble(null),
        PrefixUnaryExpressionSyntax { OperatorToken.Text: "-" } negative when Number(negative.Operand) is { } value => -value,
        _ => null,
    };

    static string Name(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        BaseTypeDeclarationSyntax type => type.Identifier.Text,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.Text,
        _ => node.Kind().ToString(),
    };

    static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}

// The same colour and scale rules for XAML attributes.
static class MarkupRules
{
    static readonly Regex Attribute = new("(?<name>[A-Za-z.]+)=\"(?<value>[^\"]*)\"");

    internal static IEnumerable<string> Check(string relative, string[] lines)
    {
        if (Scales.InTests(relative)) yield break;
        for (int i = 0; i < lines.Length; i++)
            foreach (Match match in Attribute.Matches(lines[i]))
                if (Problem(match.Groups["name"].Value, match.Groups["value"].Value, relative) is { } problem)
                    yield return $"{relative}:{i + 1}: {problem}";
    }

    static string? Problem(string name, string value, string relative)
    {
        if (Scales.HexColor.IsMatch(value) && !Scales.InTheme(relative)) return $"colour {value} outside Interface/Theme";
        var scale = name switch
        {
            "FontSize" => Scales.FontSizes,
            "Margin" or "Padding" => Scales.Spacing,
            "CornerRadius" => Scales.Radii,
            _ => null,
        };
        if (scale == null || value.StartsWith('{')) return null;
        foreach (var part in value.Split(','))
        {
            bool parsed = double.TryParse(part, System.Globalization.CultureInfo.InvariantCulture, out var number);
            if (!parsed || !scale.Contains(Math.Abs(number))) return $"{name}=\"{value}\" outside the scale";
        }
        return null;
    }
}
