using Canto34;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Humanizer;
using System.Diagnostics;

namespace bake
{
    public class Interpreter
    {
        public string Interpret(string input)
        {
            var lexer = new Lexer(input);
            var tokens = lexer.Tokens().ToList();
            var parser = new Parser();
            parser.Initialize(tokens, lexer);
            parser.Recipe();

            string ingredientList = string.Join(System.Environment.NewLine, parser.Ingredients.Select(kvp => $"{kvp.Value}{kvp.Key.UnitString} {kvp.Key.Name}."));
            string method = string.Join(System.Environment.NewLine, parser.Instructions);

            var recipe = $@"Ingredients:

{ingredientList}

Method:

{method}
";

            return recipe;
        }

        private class Lexer: LexerBase
        {
            public static readonly int Container = Lexer.NextTokenType();
            public static readonly int Repeat = Lexer.NextTokenType();
            public static readonly int Ingredient = Lexer.NextTokenType();
            public static readonly int Identifier = Lexer.NextTokenType();
            public static readonly int Dash = Lexer.NextTokenType();
            public static readonly int Comma = Lexer.NextTokenType();
            public static readonly int Digit = Lexer.NextTokenType();
            public static readonly int Op = Lexer.NextTokenType();
            public static readonly int OpParen = Lexer.NextTokenType();
            public static readonly int Cl = Lexer.NextTokenType();
            public static readonly int ClParen = Lexer.NextTokenType();
            public static readonly int WS = Lexer.NextIgnoredTokenType();

            public Lexer(string content): base(content)
            {
                this.AddPattern(Container, @"\.[A-Za-z][A-Za-z0-9]*", nameof(Container));
                this.AddKeyword(Repeat, "repeat");
                this.AddLiteral(Dash, "-");
                this.AddLiteral(Comma, ",");
                this.AddLiteral(Op, "[");
                this.AddLiteral(OpParen, "(");
                this.AddLiteral(Cl, "]");
                this.AddLiteral(ClParen, ")");
                this.AddPattern(Ingredient, @"\d+[A-Za-z]+", nameof(Ingredient));
                this.AddPattern(Digit, @"\d+", nameof(Digit));
                this.AddPattern(WS, @"\s+", nameof(WS));
                this.AddPattern(Identifier, @"[A-Za-z][A-Za-z0-9]*", nameof(Identifier));

            }
        }
        
        [DebuggerDisplay("{Code} {Name} {Units}")]
        private class IngredientType
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string Units { get; set; }
            public bool Discrete {  get { return this.Units == "1"; } }

            public object UnitString { get { return Discrete ? string.Empty : Units; } }
        }

        private class Parser : ParserBase
        {
            private Dictionary<string, IngredientType> IngredientTypes = new Dictionary<string, IngredientType>();

            public Dictionary<IngredientType, int> Ingredients { get; } = new Dictionary<IngredientType, int>();

            public List<string> Instructions { get; } = new List<string>();

            public void Recipe()
            {
                while(!EOF && this.LA1.IsOneOf(Lexer.Container, Lexer.OpParen))
                {
                    if (this.LA1.Is(Lexer.Container))
                    {
                        Stage();
                    }
                    else
                    {
                        IngredientDefinition();
                    }
                }

                if (!EOF)
                {
                    this.Error(this.LA1, "Syntax error");
                }
            }

            private void IngredientDefinition()
            {
                this.Match(Lexer.OpParen);
                var code = this.Match(Lexer.Identifier).Content;
                this.Match(Lexer.Dash);
                var description = new List<string>();
                while (!this.LA1.Is(Lexer.Comma))
                {
                    var word = this.MatchAny().Content;
                    description.Add(word);
                }
                this.Match(Lexer.Comma);
                var units = this.MatchAny().Content;
                this.Match(Lexer.ClParen);
                var item = new IngredientType
                {
                    Code = code,
                    Name = string.Join(" ", description),
                    Units = units
                };
                this.IngredientTypes.Add(item.Code, item);
            }

            private void Stage()
            {
                Container();

                StageDetails();
            }

            private void StageDetails()
            {
                while(!EOF && this.LA1.IsOneOf(Lexer.Ingredient, Lexer.Identifier, Lexer.Op))
                {
                    if (this.LA1.Is(Lexer.Ingredient))
                    {
                        Ingredient();
                    }
                    else if (this.LA1.Is(Lexer.Identifier))
                    {
                        Process();
                    }
                    else if (this.LA1.Is(Lexer.Op))
                    {
                        QuotedProgram();
                    }
                    else
                    {
                        this.Error(LA1, "Unexpected");
                    }
                }
            }

            private void Process()
            {
                var process = this.Match(Lexer.Identifier).Content;
                this.Instructions.Add( process.Titleize() + " everything.");
            }

            //LA1.Is(Lexer.Op))
            //    {
            //        QuotedProgram();
            //    }
            //    else if (this.LA1.Is(Lexer.Digit))
            //    {
            //        throw new NotImplementedException();
            //    }
            //    else
            //    {
            //        this.Error(this.LA1, "Unexpected token");
            //    }

            //}

            private void Ingredient()
            {
                var ingredient = this.Match(Lexer.Ingredient).Content;
                var digits = Regex.Match(ingredient, @"^\d+").Groups[0].Value;
                var amount = int.Parse(digits, CultureInfo.GetCultureInfo("en-GB"));
                var code = ingredient.Substring(digits.Length);
                var ingredientType = this.IngredientTypes[code];
                
                this.Instructions.Add($"Add {amount}{ingredientType.UnitString} {ingredientType.Name}.");

                int currentAmount = 0;
                if (this.Ingredients.TryGetValue(ingredientType, out currentAmount))
                {
                    currentAmount += amount;
                }
                else
                {
                    currentAmount = amount;
                }
                
                this.Ingredients[ingredientType] = currentAmount;
            }

            private void QuotedProgram()
            {
                Match(Lexer.Op);
                while (!LA1.Is(Lexer.Cl))
                {
                    this.StageDetails();
                }
                Match(Lexer.Cl);
                Match(Lexer.Repeat);
                var digits = this.Match(Lexer.Digit).Content;
                var number = int.Parse(digits, CultureInfo.GetCultureInfo("en-GB"));
                this.Instructions.Add($"(repeat {number} times)");
            }

            private void Container()
            {
                var containerName = this.Match(Lexer.Container).Content.Substring(1);
                Instructions.Add(string.Format("Use the " + containerName + "."));
            }
        }
    }
}
