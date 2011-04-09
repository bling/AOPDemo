using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace AOP.WeaverTask
{
    public class NotifyPropertyChangeWeavingTask : Task
    {
        [Required]
        public string SolutionFolder { get; set; }

        public override bool Execute()
        {
            foreach (var assembly in Directory.GetFiles(SolutionFolder, "*.dll", SearchOption.AllDirectories))
            {
                Log.LogMessage("Weaving INotifyPropertyChanged into " + assembly);
                Modify(assembly);
            }

            return true;
        }

        [Test]
        public void Run()
        {
            Modify("c:\\dev\\aop\\src\\aop.lib\\bin\\debug\\aop.lib.dll");
        }

        private static void Modify(string assemblyFile)
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(assemblyFile)))
            {
                var def = AssemblyDefinition.ReadAssembly(stream);

                var propertyChangedEventArgsCtor = GetPropertyChangedEventArgsCtor(def);
                var propertyChangedEventHandler = GetPropertyChangedEventHandler(def);
                var propertyChangedEventHandlerInvoke = GetPropertyChangedEventHandlerInvoke(def);
                var objectEqualsMethod = GetObjectEqualsMethod(def);
                var boolType = GetBool(def);

                var setMethods = from module in def.Modules
                                 from type in module.Types
                                 where type.Interfaces.Any(x => x.Name.Contains("INotifyPropertyChanged"))
                                 from p in type.Properties
                                 where p.SetMethod != null
                                 select p;

                foreach (var method in setMethods)
                {
                    var body = method.SetMethod.Body;
                    var proc = body.GetILProcessor();

                    body.Variables.Add(new VariableDefinition(propertyChangedEventHandler));
                    body.Variables.Add(new VariableDefinition(boolType));
                    body.InitLocals = true;

                    var propertyChangedField = method.DeclaringType.Fields.Single(f => f.Name == "PropertyChanged");

                    var backingFieldRef = body.Instructions.FirstOrDefault(x => x.Operand != null && x.Operand.ToString().Contains("BackingField"));
                    if (backingFieldRef == null || body.Instructions.Count > 4)
                        continue; // only support auto props

                    var backingField = (FieldReference)backingFieldRef.Operand;

                    // delete the all instructions and replace it with boilerplate

                    while (body.Instructions.Count > 0)
                        proc.Remove(body.Instructions[0]);

                    var noopRet = proc.Create(OpCodes.Nop);
                    var ret = proc.Create(OpCodes.Ret);

                    proc.Emit(OpCodes.Nop);
                    proc.Emit(OpCodes.Ldarg_0);
                    proc.Emit(OpCodes.Ldfld, backingField);
                    proc.Emit(OpCodes.Ldarg_1);

                    // use the types inequality if it exists, otherwise fall back to object.equals
                    var fieldDef = backingField.FieldType.Resolve();
                    var inEquality = fieldDef.Methods.FirstOrDefault(m => m.Name.Contains("op_Inequality"));
                    var equals = inEquality != null ? GetImportedMethod(def, inEquality) : objectEqualsMethod;

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

                if (setMethods.Any())
                {
                    def.Write(assemblyFile, new WriterParameters { WriteSymbols = true });
                }
            }
        }

        private static MethodReference GetPropertyChangedEventArgsCtor(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Import(typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
        }

        private static TypeReference GetPropertyChangedEventHandler(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Import(typeof(PropertyChangedEventHandler));
        }

        private static MethodReference GetPropertyChangedEventHandlerInvoke(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Import(typeof(PropertyChangedEventHandler).GetMethod("Invoke"));
        }

        private static MethodReference GetObjectEqualsMethod(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Import(typeof(object).GetMethod("Equals", BindingFlags.Static | BindingFlags.Public));
        }

        private static TypeReference GetBool(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Import(typeof(bool));
        }

        private static MethodReference GetImportedMethod(AssemblyDefinition assembly, MethodReference reference)
        {
            var type = Type.GetType(reference.DeclaringType.FullName);
            var method = type.GetMethod(reference.Name);
            return assembly.MainModule.Import(method);
        }
    }
}