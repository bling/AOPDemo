using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace AOP.WeaverTask
{
    public class WeavingTask : Task
    {
        private readonly IWeaver[] _weavers;

        public WeavingTask()
        {
            _weavers = new IWeaver[]
                {
                    new DependencyPropertyWeaver(),
                    new NotifyPropertyChangedWeaver(),
                };
        }

        [Required]
        public string SolutionFolder { get; set; }

        public override bool Execute()
        {
            foreach (var assembly in Directory.GetFiles(SolutionFolder, "*.dll", SearchOption.AllDirectories))
            {
                using (var stream = new MemoryStream(File.ReadAllBytes(assembly)))
                {
                    var def = AssemblyDefinition.ReadAssembly(stream);
                    bool changed = false;
                    foreach (var weaver in _weavers)
                    {
                        Log.LogMessage("Apply weaving task {0} to assembly [{1}].", weaver.GetType().Name, assembly);
                        changed |= weaver.Scan(def);
                    }

                    if (changed)
                        def.Write(assembly, new WriterParameters { WriteSymbols = true });
                }
            }

            return true;
        }
    }
}