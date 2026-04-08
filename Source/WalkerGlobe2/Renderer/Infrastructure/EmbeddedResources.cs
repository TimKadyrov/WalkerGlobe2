using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WalkerGlobe2.Renderer
{
    internal static class EmbeddedResources
    {
        public static string GetText(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string suffix = resourceName;
            int idx = resourceName.IndexOf(".Renderer.");
            if (idx >= 0)
                suffix = resourceName.Substring(idx + 1); // "Renderer.GL3x..."

            string match = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix) || n == resourceName);
            if (match == null)
                throw new FileNotFoundException(
                    $"Embedded resource not found: {resourceName}\n" +
                    $"Available: {string.Join(", ", assembly.GetManifestResourceNames().Where(n => n.EndsWith(".glsl")).Take(5))}");
            using Stream stream = assembly.GetManifestResourceStream(match);
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
