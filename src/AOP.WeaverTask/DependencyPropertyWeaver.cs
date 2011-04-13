using System;
using System.Linq;
using System.Windows;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AOP.WeaverTask
{
    public class DependencyPropertyWeaver : WeaverTask
    {
        public override bool Scan(AssemblyDefinition def)
        {
            var properties = from module in def.Modules
                             from type in module.Types
                             where type.BaseType != null
                             where type.BaseType.Name == "DependencyObject"
                             from p in type.Properties
                             where p.GetMethod != null
                             where p.SetMethod != null
                             select p;

            foreach (var prop in properties)
            {
                if (!prop.IsAutoPropertySetter())
                    continue;

                var staticCtor = prop.DeclaringType.Methods.SingleOrDefault(m => m.Name == ".cctor");
                if (staticCtor == null)
                {
                    staticCtor = new MethodDefinition(".cctor",
                                                      MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                                      def.ImportType(Type.GetType("System.Void")));
                    prop.DeclaringType.Methods.Add(staticCtor);
                    staticCtor.Body.GetILProcessor().Emit(OpCodes.Ret);
                }

                string propName = prop.Name;
                var ff = prop.DeclaringType.Fields.Single(f => f.Name.Contains("BackingField") && f.Name.Contains(propName));
                prop.DeclaringType.Fields.Remove(ff);

                var field = GetStaticDependencyPropertyField(prop.DeclaringType, propName);
                WeaveDependencyProperty(staticCtor.Body, field, prop);
                WeaveGetter(prop);
                WeaveSetter(prop);
            }

            return true;
        }

        private static void WeaveDependencyProperty(MethodBody staticCtorBody, FieldReference field, PropertyDefinition property)
        {
            var assembly = property.DeclaringType.Module.Assembly;
            var propertyType = assembly.ImportType(Type.GetType(property.PropertyType.FullName));
            var getTypeFromHandle = assembly.ImportMethod(typeof(Type).GetMethod("GetTypeFromHandle"));
            var register = assembly.ImportMethod(typeof(DependencyProperty).GetMethod("Register", new[] { typeof(string), typeof(Type), typeof(Type) }));

            // ignore previously weaved DPs
            if (staticCtorBody.Instructions.Any(i => i.Operand != null && i.Operand.ToString() == field.ToString()))
            {
                return;
            }

            if (staticCtorBody.Instructions.Last().OpCode != OpCodes.Ret)
                throw new InvalidOperationException("The last instruction should be OpCode.Ret");

            var proc = staticCtorBody.GetILProcessor();
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldstr, property.Name));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldtoken, propertyType));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, getTypeFromHandle));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Ldtoken, property.DeclaringType));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, getTypeFromHandle));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Call, register));
            proc.InsertBefore(staticCtorBody.Instructions.Last(), proc.Create(OpCodes.Stsfld, field));
        }

        private static void WeaveGetter(PropertyDefinition property)
        {
            var body = property.GetMethod.Body;
            var proc = body.GetILProcessor();
            var field = property.DeclaringType.Fields.Single(f => f.Name == property.Name + "DependencyProperty");
            var getValue = property.DeclaringType.Module.Assembly.ImportMethod(typeof(DependencyObject).GetMethod("GetValue"));

            var ldLoc0 = proc.Create(OpCodes.Ldloc_0);
            var brs = proc.Create(OpCodes.Br_S, ldLoc0);

            body.Instructions.Clear();

            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ldarg_0);
            proc.Emit(OpCodes.Ldsfld, field);
            proc.Emit(OpCodes.Call, getValue);
            proc.Emit(property.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, property.PropertyType);
            proc.Emit(OpCodes.Stloc_0);
            proc.Append(brs);
            proc.Append(ldLoc0);
            proc.Emit(OpCodes.Ret);
        }

        private static void WeaveSetter(PropertyDefinition property)
        {
            var body = property.SetMethod.Body;
            var proc = body.GetILProcessor();
            var field = property.DeclaringType.Fields.Single(f => f.Name == property.Name + "DependencyProperty");
            var setValue = property.DeclaringType.Module.Assembly.ImportMethod(typeof(DependencyObject).GetMethod("SetValue", new[] { typeof(DependencyProperty), typeof(object) }));

            body.Instructions.Clear();

            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ldarg_0);
            proc.Emit(OpCodes.Ldsfld, field);
            proc.Emit(OpCodes.Ldarg_1);
            if (property.PropertyType.IsValueType)
            {
                proc.Emit(OpCodes.Box, property.PropertyType);
            }
            proc.Emit(OpCodes.Call, setValue);
            proc.Emit(OpCodes.Nop);
            proc.Emit(OpCodes.Ret);
        }

        private static FieldReference GetStaticDependencyPropertyField(TypeDefinition type, string propertyName)
        {
            var field = type.Fields.SingleOrDefault(f => f.Name == propertyName + "DependencyProperty");
            if (field == null)
            {
                field = new FieldDefinition(propertyName + "DependencyProperty",
                                            FieldAttributes.Static | FieldAttributes.Public | FieldAttributes.InitOnly,
                                            type.Module.Import(typeof(DependencyProperty)));
                type.Fields.Add(field);
            }

            return field;
        }
    }
}