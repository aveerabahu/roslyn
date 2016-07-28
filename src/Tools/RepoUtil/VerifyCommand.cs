﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepoUtil
{
    /// <summary>
    /// This utility is used to verify the repo is in a consistent state with respect to NuGet references. 
    /// </summary>
    internal sealed class VerifyCommand : ICommand
    {
        private struct NuGetReferenceSource
        {
            internal NuGetPackage NuGetReference { get; }
            internal FileName FileName { get; }

            internal NuGetReferenceSource(NuGetPackage nugetRef, FileName fileName)
            {
                NuGetReference = nugetRef;
                FileName = fileName;
            }
        }

        private readonly string _sourcesPath;
        private readonly RepoConfig _repoConfig;
        private readonly Dictionary<string, NuGetReferenceSource> _floatingPackageMap = new Dictionary<string, NuGetReferenceSource>(StringComparer.Ordinal);

        internal VerifyCommand(RepoConfig repoConfig, string sourcesPath)
        {
            _repoConfig = repoConfig;
            _sourcesPath = sourcesPath;
        }

        public bool Run(TextWriter writer, string[] args)
        {
            return VerifyPackages(writer);
        }

        private bool VerifyPackages(TextWriter writer)
        {
            var allGood = false;
            foreach (var filePath in ProjectJsonUtil.GetProjectJsonFiles(_sourcesPath))
            {
                var fileName = FileName.FromFullPath(_sourcesPath, filePath);
                foreach (var nugetRef in ProjectJsonUtil.GetDependencies(filePath))
                {
                    if (_repoConfig.StaticPackages.Any(x => x.Name == nugetRef.Name))
                    {
                        if (!VerifyStaticPackage(writer, nugetRef, fileName))
                        {
                            allGood = false;
                        }
                    }
                    else
                    {
                        if (!VerifyFloatingPackage(writer, nugetRef, fileName))
                        {
                            allGood = false;
                        }
                    }
                }
            }

            return allGood;
        }

        private bool VerifyFloatingPackage(TextWriter writer, NuGetPackage nugetRef, FileName fileName)
        {
            NuGetReferenceSource source;
            if (_floatingPackageMap.TryGetValue(nugetRef.Name, out source))
            {
                if (source.NuGetReference.Version == nugetRef.Version)
                {
                    return true;
                }

                writer.WriteLine($"Package {nugetRef.Name} version differs in:");
                writer.WriteLine($"\t{fileName} at {nugetRef.Version}");
                writer.WriteLine($"\t{source.FileName} at {source.NuGetReference.Version}");
                return false;
            }

            _floatingPackageMap.Add(nugetRef.Name, new NuGetReferenceSource(nugetRef, fileName));
            return true;
        }

        private bool VerifyStaticPackage(TextWriter writer, NuGetPackage nugetRef, FileName fileName)
        {
            Debug.Assert(_repoConfig.StaticPackagesMap.ContainsKey(nugetRef.Name));
            var versions = _repoConfig.StaticPackagesMap[nugetRef.Name];
            if (!versions.Contains(nugetRef.Version))
            {
                writer.WriteLine($"Package {nugetRef.Name} at version {nugetRef.Version} in {fileName} is not a valid version");
                return false;
            }

            return true;
        }

    }
}
