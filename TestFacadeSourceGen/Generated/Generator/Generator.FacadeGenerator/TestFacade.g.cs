
#nullable enable
namespace TestFacadeSourceGen
{
    internal partial class TestFacade : TestFacadeSourceGen.ITest
    {
        private partial TestFacadeSourceGen.ITest GetImplementation();
        public void Test()
        {
            GetImplementation().Test();
        }
        public string TestTwo(string argument, System.Collections.Generic.ICollection<TestFacadeSourceGen.Poco> moreArguments)
        {
            return GetImplementation().TestTwo(argument, moreArguments);
        }
    }
}
