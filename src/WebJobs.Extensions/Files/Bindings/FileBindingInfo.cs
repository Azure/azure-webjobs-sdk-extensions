using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    /// <summary>
    /// The internal binding type used by <see cref="FileAttribute"/>
    /// </summary>
    internal class FileBindingInfo
    {
        /// <summary>
        /// The <see cref="FileInfo"/> for the file to bind to.
        /// </summary>
        public FileInfo FileInfo { get; set; }

        /// <summary>
        /// The <see cref="FileAttribute"/> for the corresponding parameter being bound to.
        /// </summary>
        public FileAttribute Attribute { get; set; }
    }
}
