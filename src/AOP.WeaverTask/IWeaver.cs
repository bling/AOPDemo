using Mono.Cecil;

namespace AOP.WeaverTask
{
    public interface IWeaver
    {
        /// <summary>
        /// Scans the specific assembly to see if this weaver can make any modifications.
        /// </summary>
        /// <returns><c>true</c> if modifications were made, <c>false</c> otherwise.</returns>
        bool Scan(AssemblyDefinition assembly);
    }
}