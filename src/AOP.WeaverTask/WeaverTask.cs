using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace AOP.WeaverTask
{
    public abstract class WeaverTask : Task, IWeaver
    {
        [Required]
        public string SolutionFolder { get; set; }

        public string PatternMatch { get; set; }

        protected AssemblyDefinition CurrentAssembly { get; private set; }

        public abstract bool Scan(AssemblyDefinition assembly);

        protected AssemblyDefinition Load(string path)
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
            {
                return AssemblyDefinition.ReadAssembly(stream);
            }
        }

        public override bool Execute()
        {
            Log.LogMessage("Applying weaving task " + GetType().Name + ".");

            foreach (var file in Directory.GetFiles(SolutionFolder, "*.dll", SearchOption.AllDirectories))
            {
                var def = Load(file);
                CurrentAssembly = def;
                if (Scan(def))
                    def.Write(file, new WriterParameters { WriteSymbols = true });
            }

            return true;
        }
    }
}