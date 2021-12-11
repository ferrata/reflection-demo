using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace console
{
    public interface IDynamicObject
    {
        T GetProperty<T>(string propertyName);
        void SetProperty(string propertyName, object value);
    }
    
    // public class TestClass : IDynamicObject
    // {
    //     public string Str1 { get; set; }
    //     public string Str2 { get; set; }
    //     public string Str3 { get; set; }
    //
    //     public T GetProperty<T>(string propertyName)
    //     {
    //         switch (propertyName)
    //         {
    //             case nameof(Str1):
    //                 return CastObject<T>(Str1);
    //     
    //             case nameof(Str2):
    //                 return CastObject<T>(Str2);
    //     
    //             case nameof(Str3):
    //                 return CastObject<T>(Str3);
    //
    //             default: throw new ArgumentException();
    //         }
    //     
    //     }
    //     
    //     public void SetProperty(string propertyName, object value)
    //     {
    //         switch (propertyName)
    //         {
    //             case nameof(Str1):
    //                 Str1 = (string)value;
    //                 break;
    //     
    //             case nameof(Str2):
    //                 Str2 = (string)value;
    //                 break;
    //
    //             case nameof(Str3):
    //                 Str3 = (string)value;
    //                 break;
    //     
    //             default: throw new ArgumentException();
    //         }
    //     }
    //
    //     public T CastObject<T>(object input)
    //     {
    //         return (T)input;
    //     }
    // }

    public static class SwitchCaseBuilder
    {
        public class Field
        {
            public string FieldName;
            public Type FieldType;
        }

        public static void CreateAssembly(string assemblyName)
        {
            var fields = new[]
            {
                new Field { FieldName = "Str1", FieldType = typeof(string) },
                new Field { FieldName = "Str2", FieldType = typeof(string) },
                new Field { FieldName = "Str3", FieldType = typeof(string) },
            };

            CompileAssembly(fields, assemblyName);
        }

        public static void CompileAssembly(Field[] fields, string name)
        {
            var assemblyName = new AssemblyName(name);
            var assemblyBuilder =
                AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule", $"{name}.dll");

            CompileResultType(moduleBuilder, typeof(IDynamicObject), fields);

            // save to review the compiled assembly
            assemblyBuilder.Save($"{name}.dll");
        }

        private static Type CompileResultType(ModuleBuilder moduleBuilder, Type interfaceType, Field[] fields)
        {
            var typeBuilder = moduleBuilder.DefineType(moduleBuilder.Assembly.GetName().Name,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout,
                null, new[] { interfaceType });

            // T CastObject<T>(object input)
            var castObject = typeBuilder.DefineMethod("CastObject",
                MethodAttributes.Public,
                null, new[] { typeof(object) });
            {
                var castObjectOutputParameter = castObject.DefineGenericParameters("T")[0];
                castObject.SetReturnType(castObjectOutputParameter);
                castObject.DefineParameter(1, ParameterAttributes.None, "input");

                var il = castObject.GetILGenerator();

                il.DeclareLocal(castObjectOutputParameter);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ret);
            }

            // create properties in advance so we can reference them later
            var properties = fields.ToDictionary(
                key => key.FieldName,
                value => CreateProperty(typeBuilder, value.FieldName, value.FieldType)
            );

            // T GetProperty<T>(string propertyName)
            var getProperty = typeBuilder.DefineMethod("GetProperty",
                MethodAttributes.Public | MethodAttributes.Final |
                MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                null, new[] { typeof(string) });
            {
                // define generic parameter T
                var outputParameter = getProperty.DefineGenericParameters("T")[0];
                getProperty.SetReturnType(outputParameter);

                // define name for the propertyName parameter
                getProperty.DefineParameter(1, ParameterAttributes.None, "propertyName");

                // reference to "operator ==" to compare a field name with the propertyName value
                var stringEquals =
                    typeof(string).GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public) ??
                    throw new InvalidOperationException();

                var il = getProperty.GetILGenerator();

                // declare local variables
                il.DeclareLocal(typeof(string)); // loc_0 to store input for switch / case
                il.DeclareLocal(outputParameter); // loc_1 to store result of the CastObject() call

                // define general labels, we will mark their code locations later on
                var returnLabel = il.DefineLabel(); // "return label"
                var throwLabel = il.DefineLabel(); // "throw label" for throwing ArgumentException

                // define "value labels" for each case body,
                // use map to reference them later
                var returnValueLabels = fields.ToDictionary(
                    key => key.FieldName,
                    value => il.DefineLabel()
                );

                // store propertyName in loc_0
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stloc_0);

                foreach (var field in fields)
                {
                    // check if propertyName == field.FieldName
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldstr, field.FieldName);
                    il.Emit(OpCodes.Call, stringEquals);

                    // if true, jump to the corresponding "return value" label
                    // we will mark a code location with it later (see next loop),
                    // right now we only need a reference
                    il.Emit(OpCodes.Brtrue, returnValueLabels[field.FieldName]);
                }

                // if we are here, that means the propertyName is unknown
                // jump to the "throw label" location
                il.Emit(OpCodes.Br, throwLabel);

                foreach (var field in fields)
                {
                    // mark the code with the corresponding "return value" label
                    il.MarkLabel(returnValueLabels[field.FieldName]);

                    // find a property we created before
                    // and pass its getter to the CastObject<T>() call 
                    var property = properties[field.FieldName];
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Call, property.GetMethod);
                    il.Emit(OpCodes.Call, castObject);

                    // store result in loc_1
                    il.Emit(OpCodes.Stloc_1);

                    // jump to "return label"
                    il.Emit(OpCodes.Br, returnLabel);
                }

                // mark the following code that throws with "throw label" 
                il.MarkLabel(throwLabel);
                // find ArgumentException(string) ctor
                var argumentException =
                    typeof(ArgumentException).GetConstructor(new[] { typeof(string) }) ??
                    throw new InvalidOperationException();
                // construct exception and throw
                il.Emit(OpCodes.Ldstr, "propertyName");
                il.Emit(OpCodes.Newobj, argumentException);
                il.Emit(OpCodes.Throw);

                // mark the following code with "return label" 
                il.MarkLabel(returnLabel);
                // load value from loc_1
                il.Emit(OpCodes.Ldloc_1);
                // return
                il.Emit(OpCodes.Ret);
            }

            // void SetProperty(string propertyName, object value)
            // logic is very similar to GetProperty
            var setProperty = typeBuilder.DefineMethod("SetProperty",
                MethodAttributes.Public | MethodAttributes.Final |
                MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                null, new[] { typeof(string), typeof(object) });
            {
                setProperty.DefineParameter(1, ParameterAttributes.None, "propertyName");
                setProperty.DefineParameter(2, ParameterAttributes.None, "value");

                var stringEquals =
                    typeof(string).GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public) ??
                    throw new InvalidOperationException();

                var il = setProperty.GetILGenerator();
                il.DeclareLocal(typeof(string));
                il.DeclareLocal(typeof(string));

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Stloc_0);

                var returnLabel = il.DefineLabel();
                var throwLabel = il.DefineLabel();
                var setValueLabels = fields.ToDictionary(
                    key => key.FieldName,
                    value => il.DefineLabel()
                );

                foreach (var field in fields)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldstr, field.FieldName);
                    il.Emit(OpCodes.Call, stringEquals);
                    il.Emit(OpCodes.Brtrue, setValueLabels[field.FieldName]);
                }

                il.Emit(OpCodes.Br, throwLabel);

                foreach (var field in fields)
                {
                    var property = properties[field.FieldName];
                    il.MarkLabel(setValueLabels[field.FieldName]);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Castclass, field.FieldType);
                    il.Emit(OpCodes.Call, property.SetMethod);
                    il.Emit(OpCodes.Br, returnLabel);
                }

                il.MarkLabel(throwLabel);
                var argumentException =
                    typeof(ArgumentException).GetConstructor(new[] { typeof(string) }) ??
                    throw new InvalidOperationException();

                il.Emit(OpCodes.Ldstr, "propertyName");
                il.Emit(OpCodes.Newobj, argumentException);
                il.Emit(OpCodes.Throw);

                il.MarkLabel(returnLabel);
                il.Emit(OpCodes.Ret);
            }

            typeBuilder.DefineDefaultConstructor(
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName
            );

            return typeBuilder.CreateType();
        }

        private static PropertyBuilder CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder =
                tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType,
                Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                    MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig,
                    null, new[] { propertyType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);

            return propertyBuilder;
        }
    }
}