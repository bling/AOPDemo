using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace AOP.WeaverTask
{
    public class WeaverTask : Task
    {
        [Required]
        public string OutputPath { get; set; }

        public string PatternMatch { get; set; }

        public override bool Execute()
        {
            Log.LogMessage("Applying weaving task " + GetType().Name + ".");

            foreach (var file in Directory.GetFiles(OutputPath, "*.dll", SearchOption.AllDirectories))
            {
                var bytes = File.ReadAllBytes(file);
                var assembly = Assembly.Load(bytes);
                using (var stream = new MemoryStream(bytes))
                {
                    var definition = AssemblyDefinition.ReadAssembly(stream);

                    new DependencyPropertyWeaver(assembly, definition).Weave();
                    new NotifyPropertyChangedWeaver(assembly, definition).Weave();

                    Log.LogMessage("Weaving changes into " + file);
                    definition.Write(file, new WriterParameters { WriteSymbols = true });
                }
            }

            return true;
        }
    }
}