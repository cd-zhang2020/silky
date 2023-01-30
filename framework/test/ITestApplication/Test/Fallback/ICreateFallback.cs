using System.Threading.Tasks;
using ITestApplication.Test.Dtos;

namespace ITestApplication.Test.Fallback
{
    public interface ICreateFallback
    {
        Task<TestOut> CreateOrUpdateAsync(TestInput input);
    }
}