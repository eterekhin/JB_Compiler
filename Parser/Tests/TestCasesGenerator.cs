using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Parser
{
    public class TestCasesGenerator
    {
        public void RandomExpressionToFile()
        {
            var generated = GenerateRandomExpression(100);
            File.WriteAllLines("testCases.txt", generated);
        }

        public string[] GenerateRandomExpression(int count)
        {
            var result = new string[count];

            char[] brackets = new[] {'(', ')'};
            var operators = new[]
            {
                '+', '-', '/', '*',
            };
            var variables = new[]
            {
                'x',
                'y',
                'z'
            };
            var digits = new[]
            {
                '0',
                '1',
                '2',
                '3',
                '4',
                '5',
                '6',
                '7',
                '8',
                '9',
            };

            while (count > 0)
            {
                var sb = new StringBuilder();
                var random = new Random();
                while (sb.Length < 100)
                {
                    IEnumerable<char> seq = null;
                    if (sb.Length > 0)
                    {
                        var lastSymbol = sb[^1];
                        if (digits.Contains(lastSymbol))
                        {
                            var countDigitsBefore = 0;
                            for (var i = 1; countDigitsBefore < 18 && sb.Length > i - 1; i++)
                            {
                                if (digits.Contains(sb[^i])) countDigitsBefore++;
                            }

                            seq = operators;
                            if (countDigitsBefore < 18)
                            {
                                seq = seq.Concat(digits);
                            }

                            if (DifferenceCountOpClBrackets(sb) > 0)
                                seq = seq.Concat(new[] {brackets[1]});
                        }
                        else if (variables.Contains(lastSymbol))
                        {
                            seq = operators;
                            if (DifferenceCountOpClBrackets(sb) > 0)
                                seq = seq.Concat(new[] {brackets[1]});
                        }
                        else if (operators.Contains(lastSymbol))
                        {
                            seq = variables.Concat(new[] {brackets[0]});
                            if (!digits.Contains(sb[^2]))
                                seq = seq.Concat(lastSymbol == '/' ? digits.Where(x => x != '0') : digits);
                        }
                        else if (brackets.Contains(lastSymbol))
                        {
                            if (lastSymbol == '(')
                            {
                                seq = variables.Concat(new[] {brackets[0]});
                                seq = seq.Concat(digits);
                            }
                            else
                            {
                                seq = operators;
                                if (DifferenceCountOpClBrackets(sb) > 0)
                                    seq = seq.Concat(new[] {brackets[1]});
                            }
                        }
                    }
                    else
                    {
                        seq = variables.Concat(new[] {brackets[0]});
                        seq = seq.Concat(digits);
                    }

                    var arr = seq.ToArray();
                    var randomSymbol = arr[random.Next(0, arr.Length)];
                    sb.Append(randomSymbol);
                }

                for (var i = sb.Length - 1; i > 0; i--)
                {
                    if (operators.Contains(sb[i]) || sb[i] == brackets[0])
                    {
                        sb = sb.Remove(sb.Length - 1, 1);
                    }
                    else break;
                }

                var difference = DifferenceCountOpClBrackets(sb);
                for (var i = 0; i < difference; i++)
                {
                    sb.Append(')');
                }


                var expr = sb.ToString();

                var syntaxTree = CSharpSyntaxTree.ParseText(Wrap(expr));
                var compilation = CSharpCompilation.Create(
                    "assemblyName",
                    new[] {syntaxTree},
                    new[] {MetadataReference.CreateFromFile(typeof(object).Assembly.Location)},
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using var dllStream = new MemoryStream();
                using var pdbStream = new MemoryStream();
                var emitResult = compilation.Emit(dllStream, pdbStream);
                if (emitResult.Success == false)
                {
                    continue;
                }

                result[count - 1] = expr;
                count--;
            }

            return result;
        }

        private int DifferenceCountOpClBrackets(StringBuilder sb)
        {
            return CountSymbol(sb, '(') - CountSymbol(sb, ')');
        }

        public long Test(long x, long y, long z)
        {
            return 22147482649 + 1000L;
        }

        [Fact]
        public void ldcSupport()
        {
            var lexer = new Lexer("22147482649 - 10000");
            var t = lexer.ReadAll();
            var p = new Parser(t).Parse().Single();
            var f = new ILCompiler().Compile(p);
            f(1, 1, 1);
        }

        public long Fact12(long x)
        {
            return 22147482649 + (99 + 1 + x);
        }

        public MemoryStream GetAssemblyStream(string expr, string @class = "Runner",
            string @namespace = "RunnerNamespace", string[] fields = null)
        {
            var str = Wrap(expr, @class, @namespace);
            var syntaxTree = CSharpSyntaxTree.ParseText(Wrap(expr, @class, @namespace, fields));
            var compilation = CSharpCompilation.Create(
                "assemblyName",
                new[] {syntaxTree},
                new[] {MetadataReference.CreateFromFile(typeof(object).Assembly.Location)},
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release));
            var dllStream = new MemoryStream();
            using var pdbStream = new MemoryStream();
            var result = compilation.Emit(dllStream);
            if (!result.Success)
                throw new Exception();
            dllStream.Position = 0;
            return dllStream;
        }

        private string Wrap(string expr, string @class = "Runner",
            string @namespace = "RunnerNamespace", string[] staticFields = null)
        {
            var sample = @"
namespace {namespace}
{
     public class {class}
    {
        {fields}
        public static long Run(long x, long y, long z)
        {
            return {expr};
        }
    }
}";

            if (staticFields != null)
            {
                sample = sample.Replace(
                    "{fields}",
                    string.Join(Environment.NewLine, staticFields.Select(x => $"public static long {x};")));
            }
            else
            {
                sample = sample.Replace("{fields}", "");
            }

            return sample
                .Replace("{class}", @class)
                .Replace("{namespace}", @namespace)
                .Replace("{expr}", expr);
        }

        private int CountSymbol(StringBuilder sb, char s)
        {
            var count = 0;
            for (var i = 0; i < sb.Length; i++)
                if (sb[i] == s)
                    count++;
            return count;
        }
    }
}