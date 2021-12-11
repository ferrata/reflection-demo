using System.Threading.Tasks;

namespace test.Utils
{
    public static class IlSpyCmd
    {
        public static Task<string> DecompileAsync(string assemblyPath)
        {
            return Process.RunAsync("ilspycmd", assemblyPath);
        }
    }
}