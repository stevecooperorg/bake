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
        public string Interpret(string larder, string input)
        {

            var lexer = new Lexer(larder + "\r\n" + input);
            var tokens = lexer.Tokens().ToList();
            var parser = new Parser();
            parser.Initialize(tokens, lexer);
            var recipe = parser.Recipe();

            string ingredientList = string.Join(System.Environment.NewLine, recipe.Ingredients.Select(kvp => $"{kvp.Value}{kvp.Key.UnitString} {kvp.Key.Name}."));

            string method = string.Join(System.Environment.NewLine, recipe.Instructions.Select((i,x) => $"{x+1}. " + i.Describe()));

            var result = $@"{recipe.Name}

Ingredients:

{ingredientList}

Method:

{method}
";

            return result;
        }

        private abstract class Instruction
        {
            public abstract string Describe();

            protected string ArticleFor(string noun)
            {
                return new[] { 'a', 'e', 'i', 'o', 'u' }.Contains(noun[0])
                    ? "an"
                    : "a";
            }
        }

        private class ProcessIngredientsInstruction: Instruction
        {
            public Container Ingredients;
            public string ProcessName;

            public ProcessIngredientsInstruction(string processName, Container ingredients)
            {
                this.ProcessName = processName;
                this.Ingredients = ingredients;
            }

            public override string Describe()
            {
                var ingredientList = this.Ingredients
                    .Select(ingredient => $"{ingredient.Amount}{ingredient.IngredientType.UnitString} {ingredient.IngredientType.Name}")
                    .ToList();
                
                var words = new List<string>();

                words.AddRange(this.ProcessName.Split('-'));
                if (ingredientList.Count > 0)
                {
                    words.Add(Humanizer.CollectionHumanizeExtensions.Humanize(ingredientList));
                }
                
                words.Add("in");
                words.Add(this.ArticleFor(this.Ingredients.Name));
                words.Add(this.Ingredients.Name);
             
                words[0] = words[0].Titleize();
                var msg = string.Join(" ", words) + ".";

                return msg;

            }
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
                this.AddPattern(Identifier, @"[A-Za-z][A-Za-z0-9-]*", nameof(Identifier));

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


        private class InstructionEventArgs: EventArgs
        {
            public string Message { get; private set; }
            public InstructionEventArgs(string message)
            {
                this.Message = message;
            }
        }

        private class StackParser
        {
            public event EventHandler<InstructionEventArgs> Instruction;
            private readonly Dictionary<string, IngredientType> ingredients;

            public StackParser(Dictionary<string, IngredientType> ingredients)
            {
                this.ingredients = ingredients;
            }

            private void OnInstruction(string message)
            {
                if (Instruction != null)
                {
                    Instruction(this, new InstructionEventArgs(message));
                }
            }

            public Stack<Token> Tokens { get; } = new Stack<Token>();

            public void Push(Token t)
            {
                Tokens.Push(t);
                if (t.Is(Lexer.Identifier))
                {
                    Tokens.Pop();
                    var ingredientList = new List<string>();
                    while(!Empty && Tokens.Peek().Is(Lexer.Ingredient))
                    {
                        var ingredient = Tokens.Pop().Content;

                        var digits = Regex.Match(ingredient, @"^\d+").Groups[0].Value;
                        var amount = int.Parse(digits, CultureInfo.GetCultureInfo("en-GB"));
                        var code = ingredient.Substring(digits.Length);
                        var ingredientType = this.ingredients[code];
                        var x = $"{amount}{ingredientType.UnitString} {ingredientType.Name}";
                        ingredientList.Add(x);
                    }

                    var words = new List<string>();

                    words.AddRange(t.Content.Split('-'));
                    if (ingredientList.Count > 0)
                    {
                        words.Add(Humanizer.CollectionHumanizeExtensions.Humanize(ingredientList));
                    }


                    if (!Empty && LA1.Is(Lexer.Container))
                    {
                        var container = this.Tokens.Pop();
                        words.Add("in");
                        words.Add("a");
                        words.Add(container.Content.Substring(1));
                    }
                    words[0] = words[0].Titleize();
                    var msg = string.Join(" ", words) + ".";

                    OnInstruction(msg);
                }
            }

            private Token LA1
            {
                get
                {
                    return Empty ? null : this.Tokens.Peek();
                }
            }

            private bool Empty
            {
                get
                {
                    return this.Tokens.Count == 0;
                }
            }
        }


        private class Recipe
        {
            public string Name { get; set; }
            public Dictionary<IngredientType, int> Ingredients { get; } = new Dictionary<IngredientType, int>();

            public List<Instruction> Instructions { get; } = new List<Instruction>();

        }

        private class IngredientAmount
        {
            public IngredientType IngredientType { get; set; }
            public int Amount { get; set; }
        }

        private class Container: Stack<IngredientAmount>
        {
            public string Name { get; set;  }
            public Container(string name)
            {
                this.Name = name;
            }

        }

        private class Parser : ParserBase
        {
            public Dictionary<string, IngredientType> IngredientTypes { get; } = new Dictionary<string, IngredientType>();
           
            private Recipe recipe;

            private Container currentIngredients;

            public Recipe Recipe()
            {
                this.recipe = new Interpreter.Recipe();

                while (!EOF && this.LA1.Is(Lexer.OpParen))
                {
                    IngredientDefinition();
                }

                // always start with the recipe name:
                this.recipe.Name = this.Match(Lexer.Identifier).Content.Humanize();

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

                return this.recipe;
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
                var instr = new ProcessIngredientsInstruction(process, this.currentIngredients);
                this.recipe.Instructions.Add(instr);
            }
            
            

            private void Ingredient()
            {
                var ingredient = this.Match(Lexer.Ingredient).Content;
                var digits = Regex.Match(ingredient, @"^\d+").Groups[0].Value;
                var amount = int.Parse(digits, CultureInfo.GetCultureInfo("en-GB"));
                var code = ingredient.Substring(digits.Length);

                IngredientType ingredientType;
                if (!this.IngredientTypes.TryGetValue(code, out ingredientType))
                {
                    this.Error("no known ingredient: " + code);
                }

                var ingredientAmount = new IngredientAmount
                {
                    Amount = amount,
                    IngredientType = ingredientType
                };

                this.currentIngredients.Push(ingredientAmount);
                
                int currentAmount = 0;
                if (this.recipe.Ingredients.TryGetValue(ingredientType, out currentAmount))
                {
                    currentAmount += amount;
                }
                else
                {
                    currentAmount = amount;
                }
                
                this.recipe.Ingredients[ingredientType] = currentAmount;
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
                //this.recipe.Instructions.Add($"(repeat {number} times)");
            }

            private void Container()
            {
                var containerName = this.Match(Lexer.Container).Content.Substring(1);
                this.currentIngredients = new Interpreter.Container(containerName);
            }
        }
    }
}
