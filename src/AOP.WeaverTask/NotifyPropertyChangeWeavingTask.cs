using System.ComponentModel;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace AOP.WeaverTask
{
    public class NotifyPropertyChangeWeavingTask : WeaverBase
    {
        [Test]
        public void Run()
        {
            Modify("c:\\dev\\aop\\src\\aop.lib\\bin\\debug\\aop.lib.dll");
        }

        protected override bool Modify(AssemblyDefinition def)
        {
            var propertyChangedEventArgsCtor = def.ImportPropertyChangedEventArgsCtor();
            var propertyChangedEventHandler = def.ImportType<PropertyChangedEventHandler>();
            var propertyChangedEventHandlerInvoke = def.ImportPropertyChangedEventHandlerInvoke();
            var objectEqualsMethod = def.ImportObjectEqualsMethod();
            var boolType = def.ImportType<bool>();

            var setMethods = from module in def.Modules
                             from type in module.Types
                             where type.Interfaces.Any(x => x.Name.Contains("INotifyPropertyChanged"))
                             from p in type.Properties
                             where p.SetMethod != null
                             select p;

            foreach (var method in setMethods)
            {
                var body = method.SetMethod.Body;

                var backingFieldRef = body.Instructions.FirstOrDefault(x => x.Operand != null && x.Operand.ToString().Contains("BackingField"));
                if (backingFieldRef == null || body.Instructions.Count > 4)
                    continue; // only support auto props

                body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
                body.Variables.Add(new VariableDefinition(boolType));
                body.InitLocals = true;

                var propertyChangedField = method.DeclaringType.Fields.Single(f => f.Name == "PropertyChanged");

                var backingField = (FieldReference)backingFieldRef.Operand;

                // delete the all instructions and replace it with boilerplate
                var proc = body.GetILProcessor();
                body.Instructions.Clear();

                var noopRet = proc.Create(OpCodes.Nop);
                var ret = proc.Create(OpCodes.Ret);

                proc.Emit(OpCodes.Nop);
                proc.Emit(OpCodes.Ldarg_0);
                proc.Emit(OpCodes.Ldfld, backingField);
                proc.Emit(OpCodes.Ldarg_1);

                // use the types inequality if it exists, otherwise fall back to object.equals
                var fieldDef = backingField.FieldType.Resolve();
                var inEquality = fieldDef.Methods.FirstOrDefault(m => m.Name.Contains("op_Inequality"));
                var equals = inEquality != null ? def.ImportMethod(inEquality) : objectEqualsMethod;

                if (inEquality != null)
                {
                    proc.Emit(OpCodes.Call, equals);
                    proc.Emit(OpCodes.Ldc_I4_0);
                    proc.Emit(OpCodes.Ceq);
                }
                else
                {
                    proc.Emit(OpCodes.Call, equals);
                }

                proc.Emit(OpCodes.Stloc_1);
                proc.Emit(OpCodes.Ldloc_1);
                proc.Emit(OpCodes.Brtrue_S, ret);
                proc.Emit(OpCodes.Nop);
                proc.Emit(OpCodes.Ldarg_0);
                proc.Emit(OpCodes.Ldarg_1);
                proc.Emit(OpCodes.Stfld, backingField);
                proc.Emit(OpCodes.Ldarg_0);
                proc.Emit(OpCodes.Ldfld, propertyChangedField);
                proc.Emit(OpCodes.Stloc_0);
                proc.Emit(OpCodes.Ldloc_0);
                proc.Emit(OpCodes.Ldnull);
                proc.Emit(OpCodes.Ceq);
                proc.Emit(OpCodes.Stloc_1);
                proc.Emit(OpCodes.Ldloc_1);
                proc.Emit(OpCodes.Brtrue_S, noopRet);
                proc.Emit(OpCodes.Ldloc_0);
                proc.Emit(OpCodes.Ldarg_0);
                proc.Emit(OpCodes.Ldstr, method.Name);
                proc.Emit(OpCodes.Newobj, propertyChangedEventArgsCtor);
                proc.Emit(OpCodes.Callvirt, propertyChangedEventHandlerInvoke);
                proc.Emit(OpCodes.Nop);
                proc.InsertAfter(body.Instructions.Last(), noopRet);
                proc.InsertAfter(body.Instructions.Last(), ret);
            }

            return setMethods.Any();
        }
    }
}