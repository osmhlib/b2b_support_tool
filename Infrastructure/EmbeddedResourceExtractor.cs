using System.IO;
using System.Reflection;

namespace b2b_support_tool.Infrastructure
{
    public class EmbeddedResourceExtractor
    {
        private readonly ISupportLogger _logger;
        private readonly Assembly _assembly;

        public EmbeddedResourceExtractor(ISupportLogger logger, Assembly assembly)
        {
            _logger = logger;
            _assembly = assembly;
        }

        public void Extract(string resourceName, string outputPath)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new Exception("Resource not found: " + resourceName);
            }

            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);

            _logger.Write("Extracted: " + outputPath);
        }
    }
}
