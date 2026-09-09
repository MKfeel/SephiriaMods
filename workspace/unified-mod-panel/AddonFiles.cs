using System;
using System.IO;

namespace SephiriaUnifiedModPanel
{
    // Kept independent of Unity so the file operations can be exercised outside the game.
    public static class AddonFiles
    {
        public static void CheckRoot(string root)
        {
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不操作链接目录：" + root);
        }

        public static void Move(string source, string fromRoot, string toRoot)
        {
            source = Path.GetFullPath(source);
            fromRoot = Path.GetFullPath(fromRoot).TrimEnd(Path.DirectorySeparatorChar);
            toRoot = Path.GetFullPath(toRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(source), fromRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("源目录不是 AddOns 的直接子目录。");
            CheckRoot(fromRoot);
            CheckRoot(source);
            if (Directory.Exists(toRoot)) CheckRoot(toRoot);
            string target = Path.Combine(toRoot, Path.GetFileName(source));
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException("目标存在同名目录或文件，未覆盖：" + target);
            Directory.CreateDirectory(toRoot);
            Directory.Move(source, target);
        }

    }
}
