#nullable enable

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

    public class MyAttribute : Attribute
    {
        public string Field1;

        public int Property { get; set; }
    }

    [DataContract]
    public class SourceType
    {
        [DataMember(EmitDefaultValue = false, Name = "type")]
        public string Type { get; set; } = "source";

        [DataMember(EmitDefaultValue = false, Name = "ownProperty")]
        [My(Field1 = "fieldValue", Property = 100)]
        public string OwnProperty { get; set; }
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

        private static IEnumerable<(FieldInfo field, object? value)> CollectFields(
            CustomAttributeData customAttributeData,
            IEnumerable<CustomAttributeNamedArgument> namedArguments)
        {
            var possibleFields = customAttributeData.AttributeType.GetFields();
            foreach (var customAttributeNamedArgument in namedArguments)
            {
                foreach (var fieldInfo in possibleFields)
                {
                    if (fieldInfo.Name != customAttributeNamedArgument.MemberInfo.Name)
                        continue;

                    yield return (fieldInfo, customAttributeNamedArgument.TypedValue.Value);
                }
            }
        }

        private static IEnumerable<(T member, object value)> CollectMembers<T>(
            T[] members, IEnumerable<CustomAttributeNamedArgument> namedArguments
        ) where T : MemberInfo
        {
            foreach (var namedArgument in namedArguments)
            foreach (var member in members)
            {
                if (member.Name == namedArgument.MemberInfo.Name)
                {
                    yield return (member, namedArgument.TypedValue.Value);
                }
            }
        }

        private static CustomAttributeBuilder DefineCustomAttribute(CustomAttributeData attributeData)
        {
            // based on https://stackoverflow.com/a/3916313/8607180

            var constructorArguments = attributeData.ConstructorArguments
                .Select(argument => argument.Value)
                .ToArray();

            var propertyArguments = new List<PropertyInfo>();
            var propertyArgumentValues = new List<object>();
            var fieldArguments = new List<FieldInfo>();
            var fieldArgumentValues = new List<object>();

            foreach (var argument in attributeData.NamedArguments ?? Array.Empty<CustomAttributeNamedArgument>())
            {
                var fieldInfo = argument.MemberInfo as FieldInfo;
                var propertyInfo = argument.MemberInfo as PropertyInfo;

                if (fieldInfo != null)
                {
                    fieldArguments.Add(fieldInfo);
                    fieldArgumentValues.Add(argument.TypedValue.Value);
                }
                else if (propertyInfo != null)
                {
                    propertyArguments.Add(propertyInfo);
                    propertyArgumentValues.Add(argument.TypedValue.Value);
                }
            }

            return new CustomAttributeBuilder(
                attributeData.Constructor, constructorArguments,
                propertyArguments.ToArray(), propertyArgumentValues.ToArray(),
                fieldArguments.ToArray(), fieldArgumentValues.ToArray()
            );
        }
    }
}