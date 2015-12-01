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
using System.Collections.ObjectModel;

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
            var printer = new Printer();
            var result = printer.Print(recipe);
            return result;
      }

        private class PrintContext
        {
            private StringBuilder sb = new StringBuilder();
            public int StepNumber { get; private set; } = 1; 

            public string LastMentionedContainer { get; set; }
            
            public void WriteStep(string message)
            {
                sb
                    .Append(StepNumber.ToString(CultureInfo.GetCultureInfo("en-GB")))
                    .Append(@". ")
                    .Append(message)
                    .AppendLine();

                StepNumber++;
            }

            public override string ToString()
            {
                return sb.ToString();
            }
        }

        private class Printer
        {
            public string Print(Recipe recipe)
            {
                var ingredients = recipe.Ingredients
                    .Select(kvp => new IngredientAmount { Amount = kvp.Value, IngredientType=kvp.Key })
                    .ToList();
                ingredients.Sort();
                
                string ingredientList = string.Join(
                    System.Environment.NewLine, 
                    ingredients.Select(ing => ing.ToString() + "."));

                var context = new PrintContext();

                foreach(var instruction in recipe.Instructions)
                {
                    instruction.Describe(context);
                }

                string method = context.ToString();

                var result = $@"{recipe.Name}

Ingredients:

{ingredientList}

Method:

{method}
";

                return result;

            }
        }

        private abstract class Instruction
        {
            public abstract void Describe(PrintContext context);

            protected string ArticleFor(string noun)
            {
                return new[] { 'a', 'e', 'i', 'o', 'u' }.Contains(noun[0])
                    ? "an"
                    : "a";
            }
        }

        private class RepeatOperationsInstruction : Instruction
        {
            public InstructionCollection Instructions { get; private set; }
            public int Times { get; set; }


            public RepeatOperationsInstruction(InstructionCollection instructions)
            {
                this.Instructions = instructions;
            }

            public override void Describe(PrintContext context)
            {
                var startStep = context.StepNumber;
                foreach (var instruction in Instructions)
                {
                    instruction.Describe(context);
                }
                var endStep = context.StepNumber-1;
                if (Times > 1)
                {
                    if (startStep == endStep)
                    {
                        context.WriteStep($"Repeat step {startStep} another {Times - 1} times.");
                    }
                    else
                    {
                        context.WriteStep($"Repeat step {startStep} to step {endStep} another {Times - 1} times.");
                    }
                }
            }
        }

        private class ProcessIngredientsInstruction: Instruction
        {
            public Container Ingredients;
            public string ProcessName;

            public ProcessIngredientsInstruction(string processName, Container ingredients)
            {
                this.ProcessName = processName;
                this.Ingredients = new Container(ingredients.Name);
                this.Ingredients.AddRange(ingredients);
                ingredients.Clear();
            }

            public override void Describe(PrintContext context)
            {
                var ingredientList = this.Ingredients
                    .Select(ingredient => $"{ingredient.Amount.AsFraction()}{ingredient.IngredientType.UnitString} {ingredient.IngredientType.Name}")
                    .ToList();
                
                var words = new List<string>();

                words.AddRange(this.ProcessName.Split('-'));
                if (ingredientList.Count > 0)
                {
                    words.Add(Humanizer.CollectionHumanizeExtensions.Humanize(ingredientList));
                }


                if (context.LastMentionedContainer != this.Ingredients.Name)
                {
                    words.Add("in");
                    words.Add(this.ArticleFor(this.Ingredients.Name));
                    words.Add(this.Ingredients.Name);
                    context.LastMentionedContainer = this.Ingredients.Name;
                }

                words[0] = words[0].Titleize();
                var msg = string.Join(" ", words) + ".";

                context.WriteStep(msg);
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
            public static readonly int Number = Lexer.NextTokenType();
            public static readonly int Op = Lexer.NextTokenType();
            public static readonly int OpParen = Lexer.NextTokenType();
            public static readonly int Cl = Lexer.NextTokenType();
            public static readonly int ClParen = Lexer.NextTokenType();
            public static readonly int WS = Lexer.NextIgnoredTokenType();

            public const string NumberFragmentRx = @"\d+(\.\d+)?";

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
                this.AddPattern(Ingredient, NumberFragmentRx + @"[A-Za-z]+", nameof(Ingredient));
                this.AddPattern(Number, @"\d+", nameof(Number));
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

            public override string ToString()
            {
                return base.ToString();
            }
        }


        private class InstructionEventArgs: EventArgs
        {
            public string Message { get; private set; }
            public InstructionEventArgs(string message)
            {
                this.Message = message;
            }
        }
        
        private class Recipe
        {
            public string Name { get; set; }
            public Dictionary<IngredientType, decimal> Ingredients { get; } = new Dictionary<IngredientType, decimal>();

            public InstructionCollection Instructions { get; } = new InstructionCollection();

        }

        private class IngredientAmount: IComparable<IngredientAmount>
        {
            public IngredientType IngredientType { get; set; }
            public decimal Amount { get; set; }

            public int CompareTo(IngredientAmount other)
            {
                var sign = -Math.Sign(this.Amount - other.Amount);
                if (sign != 0) { return sign; }

                return string.Compare(this.IngredientType.Name, other.IngredientType.Name);
            }

            public override string ToString()
            {
                var ingrdientName = this.IngredientType.Discrete && this.Amount != 1.0m
                    ? this.IngredientType.Name.Pluralize()
                    : this.IngredientType.Name;
                return $"{this.Amount.AsFraction()}{this.IngredientType.UnitString} {ingrdientName}";
            }
        }

        private class IngredientAmountCollection: List<IngredientAmount>
        {

        }

        private class Container: IngredientAmountCollection
        { 
            public string Name { get; set;  }
            public Container(string name)
            {
                this.Name = name;
            }
        }

        private class InstructionCollection: Collection<Instruction>
        {

        }

        private class Parser : ParserBase
        {
            public Dictionary<string, IngredientType> IngredientTypes { get; } = new Dictionary<string, IngredientType>();
            public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-GB");

            private Stack<InstructionCollection> instructionStack;

            public InstructionCollection Instructions {  get { return instructionStack.Peek(); } }

            private Recipe recipe;

            private Container currentIngredients;

            public Recipe Recipe()
            {
                this.recipe = new Interpreter.Recipe();
                this.instructionStack = new Stack<InstructionCollection>();
                this.instructionStack.Push(this.recipe.Instructions);

                // the larder; defines the standard 'library' of ingredients 
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
                        Step();
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

            private void Step()
            {
                Container();

                StepDetails();
            }

            private void StepDetails()
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
                this.Instructions.Add(instr);
            }
            
            

            private void Ingredient()
            {
                var ingredient = this.Match(Lexer.Ingredient).Content;
                var digits = Regex.Match(ingredient, Lexer.NumberFragmentRx).Groups[0].Value;
                var amount = decimal.Parse(digits, this.Culture);
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

                this.currentIngredients.Add(ingredientAmount);
                
                decimal currentAmount = 0;
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

               
                var program = new InstructionCollection();
                this.instructionStack.Push(program);

                while (!LA1.Is(Lexer.Cl))
                {
                    this.StepDetails();
                }
                this.instructionStack.Pop();
                
                Match(Lexer.Cl);
                Match(Lexer.Repeat);
                var digits = this.Match(Lexer.Number).Content;
                var number = int.Parse(digits, this.Culture);

                var repeater = new RepeatOperationsInstruction(program) { Times = number };

                this.Instructions.Add(repeater);
            }

            private void Container()
            {
                var containerName = this.Match(Lexer.Container).Content.Substring(1);
                this.currentIngredients = new Interpreter.Container(containerName);
            }
        }
    }
}
