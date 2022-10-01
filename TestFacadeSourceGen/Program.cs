// See https://aka.ms/new-console-template for more information


using TestFacadeSourceGen;

partial class Program
{
    static void Main(string[] args)
    {
        var facade = new TestFacade();

        for (var i = 0; i < 10; i++)
        {
            Console.WriteLine(facade.TestTwo(null, null));
        }

        Console.ReadLine();
    }
}