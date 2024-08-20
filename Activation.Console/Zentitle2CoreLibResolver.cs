using System.Reflection;
using System.Runtime.InteropServices;

namespace Activation.Console
{
    public static class Zentitle2CoreLibResolver
    {
        private const string CoreLibName = "Zentitle2Core";

        /// <summary>
        /// Default path to the core library
        /// </summary>
        private const string CoreLibPath = "Zentitle2CoreLibPlaceholder";

        /// <summary>
        /// Should be called once to initialize the resolver.
        /// </summary>
        public static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != CoreLibName)
            {
                // Fallback to default import resolver.
                return IntPtr.Zero;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64 ||
                    RuntimeInformation.OSArchitecture == Architecture.X86)
                {
                    return NativeLibrary.Load($"{CoreLibPath}/MacOS_x86_64/libZentitle2Core.dylib");
                }

                return NativeLibrary.Load($"{CoreLibPath}/MacOS_arm64/libZentitle2Core.dylib");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeLibrary.Load($"{CoreLibPath}/Windows_x86_64/Zentitle2Core.dll");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                return NativeLibrary.Load($"{CoreLibPath}/Linux_x86_64/libZentitle2Core.so");
            }

            // Otherwise, fallback to default import resolver.
            return IntPtr.Zero;
        }
    }
}