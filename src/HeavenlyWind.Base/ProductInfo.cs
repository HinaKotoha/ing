﻿using System.IO;

namespace Sakuno.KanColle.Amatsukaze
{
    public static class ProductInfo
    {
        public const string AppName = "いんてりじぇんと連装砲くん";
        public const string ProductName = "Intelligent Naval Gun";

        public const string AssemblyVersionString = "0.1.15.11";

        public static string Version => AssemblyVersionString;
        public static string ReleaseCodeName => "Braindrive";
        public static string ReleaseDate => "2018.11.20";
        public static string ReleaseType => "Release";

        public const string UserAgent = "ING/" + AssemblyVersionString;

        public static string RootDirectory { get; } = Path.GetDirectoryName(typeof(ProductInfo).Assembly.Location);
    }
}
