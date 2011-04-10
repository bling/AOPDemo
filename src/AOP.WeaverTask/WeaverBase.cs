using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace AOP.WeaverTask
{
    public abstract class WeaverBase : Task
    {
        [Required]
        public string SolutionFolder { get; set; }

        public override bool Execute()
        {
            foreach (var assembly in Directory.GetFiles(SolutionFolder, "*.dll", SearchOption.AllDirectories))
            {
                Log.LogMessage("Apply weaving task {0} to assembly [{1}].", GetType().Name, assembly);
                Modify(assembly);
            }

            return true;
        }

        protected void Modify(string path)
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
            {
                var def = AssemblyDefinition.ReadAssembly(stream);
                if (Modify(def))
                    def.Write(path, new WriterParameters { WriteSymbols = true });
            }
        }

        protected abstract bool Modify(AssemblyDefinition assembly);
    }
}