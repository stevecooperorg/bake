using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace bake.tests
{
    [TestFixture]
    public class InterpreterTests
    {
        Interpreter interpreter;
        [SetUp]
        public void Init()
        {
            this.interpreter = new Interpreter();
        }

        public static IEnumerable<TestDetails> Recipes()
        {
            var dir = new FileInfo(new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath).DirectoryName;
            var filePath = Path.Combine(dir, "Scripts\\Recipes.txt");
            Assert.IsTrue(File.Exists(filePath), filePath);
            string fileContent = File.ReadAllText(filePath);
            
            var breakRx = new Regex(@"---+");
            var pieces = breakRx.Split(fileContent);

            Assert.IsTrue(pieces.Length % 2 == 0, "should be an even number of pieces in the test file but found " + pieces.Length);

            var result = new List<TestDetails>();
            for(var i =0; i < pieces.Length; i+=2)
            {
                var detail = new TestDetails
                {
                    Recipe = pieces[i].Trim(),
                    Expected = pieces[i + 1].Trim()
                };
                result.Add(detail);
            }

            return result;
        }


        public class TestDetails
        {
            public string Recipe; public string Expected;
            public override string ToString()
            {
                return Recipe;
            }
        }

        [Test]
        [TestCaseSource("Recipes")]
        public void Interpreter_CanParseRecipe(TestDetails test)
        {
            var larderPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts\\Larder.txt");
            Assert.IsTrue(File.Exists(larderPath));
            var larder = File.ReadAllText(larderPath);

            var directions = test.Recipe;
            var expected = test.Expected;
            var result = this.interpreter.Interpret(larder, directions).Trim();
            Console.WriteLine("--------------- result");
            Console.WriteLine("");
            Console.WriteLine(result);
            Console.WriteLine("");
            Console.WriteLine("result ------ expected");
            Console.WriteLine("");
            Console.WriteLine(expected);
            Console.WriteLine("");
            Console.WriteLine("expected -------------");
            Console.WriteLine("");
            Console.WriteLine("");

            Assert.AreEqual(expected, result);
        }
    }
}
