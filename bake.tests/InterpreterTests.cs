using NUnit.Framework;
using System;
using System.IO;

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

        [Test]
        [TestCase("SpongeCake")]
        [TestCase("CaramelSauce")]
        public void Interpreter_CanParseRecipe(string name)
        {
            var larderPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts\\Larder.txt");
            Assert.IsTrue(File.Exists(larderPath));
            var larder = File.ReadAllText(larderPath);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts\\" + name + ".txt");
            Assert.IsTrue(File.Exists(filePath));
            string fileContent = File.ReadAllText(filePath);

            var lineAt = fileContent.IndexOf("----");
            var lineEnd = fileContent.LastIndexOf("-----") + 5;

            var directions = larder + System.Environment.NewLine + fileContent.Substring(0, lineAt).Trim();
            var expected = fileContent.Substring(lineEnd).Trim();
            var result = this.interpreter.Interpret(directions).Trim();
            Console.WriteLine(result);
            Assert.AreEqual(expected, result);
        }

        


    }
}
