using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace SephiriaUnifiedModPanel
{
    public sealed class PluginFile
    {
        public string Path;
        public string Name;
        public bool Enabled;
        public readonly List<string> Guids = new List<string>();
        public readonly List<string> HardDependencies = new List<string>();
    }

    public static class PluginFiles
    {
        public const string DisabledSuffix = ".unified-disabled";
        private const string ManagerGuid = "com.sephiria.unifiedmodpanel";

        private static IEnumerable<string> Files(string root)
        {
            AddonFiles.CheckRoot(root);
            foreach (string file in Directory.GetFiles(root))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 &&
                    (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".dll" + DisabledSuffix, StringComparison.OrdinalIgnoreCase)))
                    yield return file;
            foreach (string folder in Directory.GetDirectories(root))
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0)
                    foreach (string file in Files(folder)) yield return file;
        }

        private static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> source)
        {
            foreach (var type in source)
            {
                yield return type;
                foreach (var nested in Types(type.NestedTypes)) yield return nested;
            }
        }

        public static List<PluginFile> Scan(string root, Action<string> log)
        {
            var result = new List<PluginFile>();
            if (!Directory.Exists(root)) return result;
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "..", "core")));
                foreach (string path in Files(root))
                {
                    try
                    {
                        resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(path));
                        using (var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver }))
                        {
                            var item = new PluginFile { Path = path, Enabled = path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) };
                            var names = new List<string>();
                            foreach (var type in Types(assembly.MainModule.Types))
                            {
                                var plugin = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
                                if (plugin == null || plugin.ConstructorArguments.Count < 3) continue;
                                item.Guids.Add((string)plugin.ConstructorArguments[0].Value);
                                names.Add((string)plugin.ConstructorArguments[1].Value);
                                foreach (var dependency in type.CustomAttributes.Where(a => a.AttributeType.FullName == "BepInEx.BepInDependency"))
                                {
                                    int flags = 1;
                                    if (dependency.ConstructorArguments.Count > 1 && dependency.ConstructorArguments[1].Type.FullName != "System.String")
                                        flags = Convert.ToInt32(dependency.ConstructorArguments[1].Value);
                                    if ((flags & 1) != 0) item.HardDependencies.Add((string)dependency.ConstructorArguments[0].Value);
                                }
                            }
                            if (item.Guids.Count == 0) continue; // Dependency DLLs are not toggleable mods.
                            item.Name = string.Join(" + ", names) + (names.Count > 1 ? "（共用 DLL）" : "");
                            result.Add(item);
                        }
                    }
                    catch (BadImageFormatException) { } // Native DLL.
                    catch (Exception e) { log?.Invoke("读取插件元数据失败：" + path + "：" + e.Message); }
                }
            }
            return result;
        }

        public static void SetEnabled(PluginFile selected, bool enabled, string root)
        {
            string path = System.IO.Path.GetFullPath(selected.Path);
            root = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("插件不在 plugins 目录内。");
            AddonFiles.CheckRoot(root);
            for (string current = path; !string.Equals(current, root, StringComparison.OrdinalIgnoreCase); current = System.IO.Path.GetDirectoryName(current))
                AddonFiles.CheckRoot(current);
            // Re-read before mutation so stale UI cannot bypass dependency or self protection.
            var all = Scan(root, message => { throw new IOException(message); });
            var item = all.SingleOrDefault(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            if (item == null) throw new IOException("插件文件已变化，请刷新。");
            if (item.Guids.Contains(ManagerGuid)) throw new IOException("不能从面板禁用面板自身。");
            if (item.Enabled == enabled) return;
            if (!enabled)
            {
                var dependents = all.Where(p => p.Enabled && p.Path != item.Path && p.HardDependencies.Any(d => item.Guids.Contains(d))).ToList();
                if (dependents.Count != 0) throw new IOException("请先禁用依赖它的插件：" + string.Join("、", dependents.Select(p => p.Name)));
            }
            else
            {
                var present = new HashSet<string>(all.Where(p => p.Enabled || p.Path == item.Path).SelectMany(p => p.Guids));
                var missing = item.HardDependencies.Where(d => !present.Contains(d)).ToList();
                if (missing.Count != 0) throw new IOException("请先恢复前置插件：" + string.Join("、", missing));
                if (all.Any(p => p.Enabled && p.Path != item.Path && p.Guids.Intersect(item.Guids).Any())) throw new IOException("存在相同 GUID 的已启用插件，未恢复重复副本。");
            }
            string target = enabled ? path.Substring(0, path.Length - DisabledSuffix.Length) : path + DisabledSuffix;
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("目标文件已存在，未覆盖：" + target);
            File.Move(path, target);
        }
    }
}
