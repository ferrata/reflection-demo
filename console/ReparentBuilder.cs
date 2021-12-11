using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace console
{
    public interface IPaymentResponseAction
    {
        [JsonProperty(PropertyName = "type")]
        string Type { get; set; }
    }

    [DataContract]
    public class PaymentResponseAction : IPaymentResponseAction
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }
    }

    public class SourceType
    {
        
    }

    internal static class ReparentBuilder
    {
        public static void CreateAssembly(string assemblyName)
        {
            var name = new AssemblyName(assemblyName);
            var assemblyBuilder =
                AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule", $"{name}.dll");

            GenerateReparentedType(moduleBuilder, typeof(SourceType), typeof(PaymentResponseAction));

            assemblyBuilder.Save($"{name}.dll");
        }

        private static Type GenerateReparentedType(ModuleBuilder moduleBuilder, Type originalType, Type parent)
        {
            var typeBuilder = moduleBuilder.DefineType(originalType.Name, TypeAttributes.Public, parent);

            foreach (var property in originalType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var newProperty = typeBuilder
                    .DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

                var getMethod = property.GetMethod;
                if (getMethod is not null)
                {
                    var getMethodBuilder = typeBuilder
                        .DefineMethod(getMethod.Name, getMethod.Attributes, getMethod.ReturnType, Type.EmptyTypes);
                    getMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
                    newProperty.SetGetMethod(getMethodBuilder);
                }

                var setMethod = property.SetMethod;
                if (setMethod is not null)
                {
                    var setMethodBuilder = typeBuilder
                        .DefineMethod(setMethod.Name, setMethod.Attributes, setMethod.ReturnType, Type.EmptyTypes);
                    setMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
                    newProperty.SetSetMethod(setMethodBuilder);
                }

                var customAttributes = CustomAttributeData.GetCustomAttributes(property).ToArray();
                foreach (var customAttributeData in customAttributes)
                {
                    newProperty.SetCustomAttribute(DefineCustomAttribute(customAttributeData));
                }
            }

            var type = typeBuilder.CreateType();
            return type ?? throw new InvalidOperationException($"Unable to generate a re-parented type for {originalType}.");
        }

        private static CustomAttributeBuilder DefineCustomAttribute(CustomAttributeData customAttributeData)
        {
            // based on https://stackoverflow.com/questions/2365470/using-reflection-emit-to-copy-a-custom-attribute-to-another-method
            var namedFieldValues = new List<object?>();
            var fields = new List<FieldInfo>();
            var constructorArguments = customAttributeData
                .ConstructorArguments
                .Select(ctorArg => ctorArg.Value)
                .ToList();

            if (customAttributeData.NamedArguments.Count > 0)
            {
                var possibleFields = customAttributeData.GetType().GetFields();
                foreach (var customAttributeNamedArgument in customAttributeData.NamedArguments)
                {
                    foreach (var fieldInfo in possibleFields)
                    {
                        if (string.Compare(fieldInfo.Name, customAttributeNamedArgument.MemberInfo.Name,
                            StringComparison.Ordinal) != 0)
                            continue;

                        fields.Add(fieldInfo);
                        namedFieldValues.Add(customAttributeNamedArgument.TypedValue.Value);
                    }
                }
            }

            return namedFieldValues.Count > 0
                ? new CustomAttributeBuilder(
                    customAttributeData.Constructor,
                    constructorArguments.ToArray(), fields.ToArray(),
                    namedFieldValues.ToArray())
                : new CustomAttributeBuilder(customAttributeData.Constructor, constructorArguments.ToArray());
        }
    }
}