using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using test.Utils;

namespace test
{
    public class Tests : Fixture
    {
        [Test]
        public async Task TestSwitchCaseBuilderAsync()
        {
            var path = Path.Combine(GetOwnDebugDir(), "SwitchCaseResult.dll");
            var code = await IlSpyCmd.DecompileAsync(path);
            // await Console.Out.WriteLineAsync(code);

            Assert.That(code.Wash(), Is.EqualTo(@"
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using console;

[assembly: AssemblyVersion(""0.0.0.0"")]
[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
public class SwitchCaseResult : IDynamicObject
{
    private string _Str1;

    private string _Str2;

    private string _Str3;

    public string Str1
    {
        get
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            return _Str1;
        }
        set
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            _Str1 = value;
        }
    }

    public string Str2
    {
        get
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            return _Str2;
        }
        set
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            _Str2 = value;
        }
    }

    public string Str3
    {
        get
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            return _Str3;
        }
        set
        {
            //Error decoding local variables: Signature type sequence must have at least one element.
            _Str3 = value;
        }
    }

    public T CastObject<T>(object input)
    {
        return (T)input;
    }

    public T GetProperty<T>(string propertyName)
    {
        return propertyName switch
        {
            ""Str1"" => CastObject<T>(Str1), 
            ""Str2"" => CastObject<T>(Str2), 
            ""Str3"" => CastObject<T>(Str3), 
            _ => throw new ArgumentException(""propertyName""), 
        };
    }

    public void SetProperty(string propertyName, object value)
    {
        switch (propertyName)
        {
        case ""Str1"":
            Str1 = (string)value;
            break;
        case ""Str2"":
            Str2 = (string)value;
            break;
        case ""Str3"":
            Str3 = (string)value;
            break;
        default:
            throw new ArgumentException(""propertyName"");
        }
    }
}
".Wash()));
        }
    }
}