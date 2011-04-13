using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AOP.WeaverTask
{
    public class EmbeddedDependencyPropertyWeaver : IWeaver
    {
        private AssemblyDefinition Assembly { get; set; }

        public bool Scan(AssemblyDefinition def)
        {
            Assembly = def.MainModule.Assembly;

            var properties = from module in def.Modules
                             from type in module.Types
                             from p in type.Properties
                             where p.IsAutoPropertySetter()
                             select p;

            var types = properties.GroupBy(p => p.DeclaringType);

            foreach (var t in types)
            {
                WeaveEmbeddedClass(t.Key, t);
            }

            return true;
        }

        private void WeaveEmbeddedClass(TypeDefinition type, IEnumerable<PropertyDefinition> properties)
        {
            if (type.NestedTypes.Any(t => t.Name == "__EMBEDDED_DP"))
                return;

            var embeddedType = GenerateEmbeddedType(type);
            var staticCtor = embeddedType.Methods.Single(m => m.Name == ".cctor");

            var embeddedField = new FieldDefinition("__embedded_dp", FieldAttributes.Private | FieldAttributes.InitOnly, embeddedType);
            type.Fields.Add(embeddedField);

            WeaveEmbeddedInitializeIntoCtor(type, embeddedType, embeddedField);

            foreach (var prop in properties)
            {
                // weave the fields into the class
                var field = GetStaticDependencyPropertyField(embeddedType, prop.Name);
                WeaveDependencyProperty(staticCtor.Body, field, prop);

                var ff = prop.DeclaringType.Fields.Single(f => f.Name.Contains("BackingField") && f.Name.Contains(prop.Name));
                prop.DeclaringType.Fields.Remove(ff);
                prop.SetMethod.CustomAttributes.Clear();
                prop.GetMethod.CustomAttributes.Clear();

                WeaveGetter(prop, embeddedField, embeddedType);
                WeaveSetter(prop, embeddedField, embeddedType);
            }

            type.NestedTypes.Add(embeddedType);
        }

        private TypeDefinition GenerateEmbeddedType(TypeDefinition type)
        {
            var embeddedType = new TypeDefinition(
                string.Empty,
                "__EMBEDDED_DP",
                TypeAttributes.Public | TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit);

            embeddedType.BaseType = Assembly.ImportType<DependencyObject>();

            // generate default/static constructors

            var ctor = new MethodDefinition(".ctor",
                                            MethodAttributes.FamANDAssem | MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                            type.Module.Assembly.ImportType(Type.GetType("System.Void")));

            var proc = ctor.Body.GetILProcessor();
            proc.Emit(OpCodes.Ldarg_0);
            proc.Emit(OpCodes.Call, Assembly.ImportMethod(typeof(DependencyObject).GetConstructors()[0]));
            proc.Emit(OpCodes.Ret);
            embeddedType.Methods.Add(ctor);

            var staticCtor = new MethodDefinition(".cctor",
                                                  MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                                  type.Module.Assembly.ImportType(Type.GetType("System.Void")));
            embeddedType.Methods.Add(staticCtor);
            staticCtor.Body.GetILProcessor().Emit(OpCodes.Ret);

            return embeddedType;
        }

        private static void WeaveEmbeddedInitializeIntoCtor(TypeDefinition type, TypeDefinition embeddedType, FieldDefinition embeddedField)
        {
            var ctor = type.Methods.Single(m => m.Name == ".ctor");
            var body = ctor.Body;
            var proc = body.GetILProcessor();
            var embeddedCtor = embeddedType.Methods.Single(m => m.Name == ".ctor");

            // the op codes are in reverse order, since we want to add it to the beginning of the ctor
            proc.InsertBefore(body.Instructions.First(), proc.Create(OpCodes.Stfld, embeddedField));
            proc.InsertBefore(body.Instructions.First(), proc.Create(OpCodes.Newobj, embeddedCtor));
            proc.InsertBefore(body.Instructions.First(), proc.Create(OpCodes.Ldarg_0));
        }

        private void WeaveDependencyProperty(MethodBody staticCtorBody, FieldReference field, PropertyDefinition property)
        {
            var propertyType = Assembly.ImportType(Type.GetType(property.PropertyType.FullName));
            var getTypeFromHandle = Assembly.ImportMethod(typeof(Type).GetMethod("GetTypeFromHandle"));
            var register = Assembly.ImportMethod(typeof(DependencyProperty).GetMethod("Register", new[] { typeof(string), typeof(Type), typeof(Type) }));

            if (staticCtorBody.Instructions.Last().OpCode != OpCodes.Ret)
                throw new InvalidOperationException("The last instruction should be OpCode.Ret");

            var proc = staticCtorBody.GetILProcessor();
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldstr, property.Name));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldtoken, propertyType));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, getTypeFromHandle));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldtoken, field.DeclaringType));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, getTypeFromHandle));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, register));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Stsfld, field));
        }

        private void WeaveGetter(PropertyDefinition property, FieldDefinition embeddedField, TypeDefinition type)
        {
            var body = property.GetMethod.Body;
            var proc = body.GetILProcessor();
            var dpField = type.Fields.Single(f => f.Name == property.Name + "DependencyProperty");
            var getValue = Assembly.ImportMethod(typeof(DependencyObject).GetMethod("GetValue"));

            var ldLoc0 = proc.Create(OpCodes.Ldloc_0);
            var brs = proc.Create(OpCodes.Br_S, ldLoc0);

            body.Instructions.Clear();

            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ldarg_0);
            proc.Emit(OpCodes.Ldfld, embeddedField);
            proc.Emit(OpCodes.Ldsfld, dpField);
            proc.Emit(OpCodes.Callvirt, getValue);
            proc.Emit(property.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, property.PropertyType);
            proc.Emit(OpCodes.Stloc_0);
            proc.Append(brs);
            proc.Append(ldLoc0);
            proc.Emit(OpCodes.Ret);
        }

        private static void WeaveSetter(PropertyDefinition property, FieldDefinition embeddedField, TypeDefinition type)
        {
            var body = property.SetMethod.Body;
            var proc = body.GetILProcessor();
            var dpField = type.Fields.Single(f => f.Name == property.Name + "DependencyProperty");
            var setValue = property.DeclaringType.Module.Assembly.ImportMethod(typeof(DependencyObject).GetMethod("SetValue", new[] { typeof(DependencyProperty), typeof(object) }));

            body.Instructions.Clear();

            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ldarg_0);
            proc.Emit(OpCodes.Ldfld, embeddedField);
            proc.Emit(OpCodes.Ldsfld, dpField);
            proc.Emit(OpCodes.Ldarg_1);
            if (property.PropertyType.IsValueType)
            {
                proc.Emit(OpCodes.Box, property.PropertyType);
            }
            proc.Emit(OpCodes.Callvirt, setValue);
            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ret);
        }

        private FieldReference GetStaticDependencyPropertyField(TypeDefinition type, string propertyName)
        {
            var field = type.Fields.SingleOrDefault(f => f.Name == propertyName + "DependencyProperty");
            if (field == null)
            {
                field = new FieldDefinition(propertyName + "DependencyProperty",
                                            FieldAttributes.Static | FieldAttributes.Public | FieldAttributes.InitOnly,
                                            Assembly.ImportType<DependencyProperty>());
                type.Fields.Add(field);
            }

            return field;
        }
    }
}