using Kama.DatabaseModel;
using NUnit.Framework;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestSpMapper()
        {
            var conn = new
            {
                connString = "Data Source=46.225.116.210;Initial Catalog=Kama.Mefa.Azmoon;User ID=kama; Password=K@maPMGs@dUL98"
                ,
                nameSpace = "Kama.Mefa.Azmoon.DataSource"
            };
            var generator = new Generator(conn.connString, "dbo");
            string generatedText = generator.Generate(conn.nameSpace);
        }
    }
}