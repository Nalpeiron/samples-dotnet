using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;

namespace SharedActivation.Console
{
    public static class Zentitle2CoreLibResolver
    {
        private const string CoreLibName = "Zentitle2Core";

        /// <summary>
        /// Default path to the core library
        /// </summary>
        private const string CoreLibPath = "Zentitle2Core";

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
                    return NativeLibrary.Load(GetCoreLibPath("MacOS_x86_64", "libZentitle2Core.dylib"));
                }

                return NativeLibrary.Load(GetCoreLibPath("MacOS_arm64", "libZentitle2Core.dylib"));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeLibrary.Load(GetCoreLibPath("Windows_x86_64", "Zentitle2Core.dll"));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                if (IsAlpineLinux())
                {
                    if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                    {
                        return NativeLibrary.Load(GetCoreLibPath("Linux_alpine_aarch64", "libZentitle2Core.so"));
                    }

                    return NativeLibrary.Load(GetCoreLibPath("Linux_alpine_x86_64", "libZentitle2Core.so"));
                }

                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    return NativeLibrary.Load(GetCoreLibPath("Linux_aarch64", "libZentitle2Core.so"));
                }

                return NativeLibrary.Load(GetCoreLibPath("Linux_x86_64", "libZentitle2Core.so"));
            }

            // Otherwise, fallback to default import resolver.
            return IntPtr.Zero;
        }

        private static string GetCoreLibPath(string platformDirectory, string fileName)
        {
            return Path.Combine(AppContext.BaseDirectory, CoreLibPath, platformDirectory, fileName);
        }

        private static bool IsAlpineLinux()
        {
            return File.Exists("/etc/alpine-release");
        }
    }
}
