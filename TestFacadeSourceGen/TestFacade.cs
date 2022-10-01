using FacadeGenerator;

namespace TestFacadeSourceGen
{
    public class Poco {};

    public interface ITest
    {
        void Test();

        string TestTwo(string argument, ICollection<Poco> moreArguments);
    }

    public class Impl1 : ITest
    {
        public void Test()
        {
            throw new NotImplementedException();
        }

        public string TestTwo(string argument, ICollection<Poco> moreArguments)
        {
            return nameof(Impl1);
        }
    }

    public class Impl2 : ITest
    {
        public void Test()
        {
            throw new NotImplementedException();
        }

        public string TestTwo(string argument, ICollection<Poco> moreArguments)
        {
            return nameof(Impl2);
        }
    }

    internal partial class TestFacade : IFacadeGenerator<ITest>
    {
        int counter = 0;
        private readonly Impl1 _impl1;
        private readonly Impl2 _impl2;

        public TestFacade()
        {
            _impl1 = new Impl1();
            _impl2 = new Impl2();
        }

        private partial ITest GetImplementation()
        {
            if (counter++ % 2 == 0)
            {
                return _impl1;
            }

            return _impl2;
        }
    }
}
