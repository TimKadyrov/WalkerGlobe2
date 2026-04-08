using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WalkerGlobe2.Scene
{
    internal static class EmbeddedResources
    {
        public static string GetText(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            // Strip the old assembly prefix (e.g. "WalkerGlobe.Scene.") and match by suffix
            string suffix = resourceName;
            int firstDotAfterNs = resourceName.IndexOf(".Scene.");
            if (firstDotAfterNs >= 0)
                suffix = resourceName.Substring(firstDotAfterNs + 1); // "Scene.Globes..."

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
