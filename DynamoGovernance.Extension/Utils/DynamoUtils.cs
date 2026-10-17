using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamo.Graph.Workspaces;
using DynamoGovernance.Core.Models;

namespace DynamoGovernance.Extension.Utils
{
    internal class DynamoUtils
    {
        internal static PackageReferenceData[] GetPackages(
            HomeWorkspaceModel workspace)
        {
            try
            {
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

                Type workspaceType = workspace.GetType().BaseType
                    ?? workspace.GetType();

                object? dependencies = workspaceType
                    .GetMethod(
                        "ComputeNodeLibraryDependencies",
                        flags)?
                    .Invoke(workspace, null);

                dependencies ??= workspaceType
                    .GetProperty(
                        "NodeLibraryDependencies",
                        flags)?
                    .GetValue(workspace);

                if (dependencies is not System.Collections.IEnumerable items)
                {
                    return [];
                }

                List<PackageReferenceData> packages = [];

                foreach (object dependency in items)
                {
                    Type dependencyType = dependency.GetType();
                    string? referenceType = dependencyType
                        .GetProperty("ReferenceType")?
                        .GetValue(dependency)?
                        .ToString();

                    if (!string.IsNullOrWhiteSpace(referenceType) &&
                        !string.Equals(
                            referenceType,
                            "Package",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(referenceType) &&
                        !dependencyType.Name.Contains(
                            "PackageDependency",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? name = dependencyType
                        .GetProperty("Name")?
                        .GetValue(dependency)?
                        .ToString();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string? version = dependencyType
                        .GetProperty("Version")?
                        .GetValue(dependency)?
                        .ToString();

                    string? dependencyPath = dependencyType
                        .GetProperty(
                            "Path",
                            flags)?
                        .GetValue(dependency)?
                        .ToString();

                    PackageManifest manifest =
                        ReadPackageManifest(dependencyPath);

                    packages.Add(new PackageReferenceData
                    {
                        Name = name,
                        Version = version ?? manifest.Version,
                        NodeCount = GetDependentNodeCount(
                            dependency,
                            flags),
                        Description = manifest.Description,
                        License = manifest.License,
                        RepositoryUrl = manifest.RepositoryUrl,
                        SiteUrl = manifest.SiteUrl
                    });
                }

                return packages
                    .GroupBy(package => new
                    {
                        package.Name,
                        package.Version
                    })
                    .Select(group =>
                    {
                        PackageReferenceData package = group.First();

                        return new PackageReferenceData
                        {
                            Name = package.Name,
                            Version = package.Version,
                            NodeCount = group.Sum(item => item.NodeCount),
                            Description = package.Description,
                            License = package.License,
                            RepositoryUrl = package.RepositoryUrl,
                            SiteUrl = package.SiteUrl
                        };
                    })
                    .OrderBy(package => package.Name)
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }


        private static int GetDependentNodeCount(
            object dependency,
            System.Reflection.BindingFlags flags)
        {
            object? nodes = dependency
                .GetType()
                .GetProperty(
                    "Nodes",
                    flags)?
                .GetValue(dependency);

            if (nodes is System.Collections.ICollection collection)
            {
                return collection.Count;
            }

            if (nodes is not System.Collections.IEnumerable items)
            {
                return 0;
            }

            int count = 0;

            foreach (object _ in items)
            {
                count++;
            }

            return count;
        }


        private static PackageManifest ReadPackageManifest(
            string? dependencyPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dependencyPath))
                {
                    return new PackageManifest();
                }

                DirectoryInfo? directory = File.Exists(dependencyPath)
                    ? new FileInfo(dependencyPath).Directory
                    : new DirectoryInfo(dependencyPath);

                for (int level = 0;
                    directory is not null && level < 8;
                    level++, directory = directory.Parent)
                {
                    string manifestPath = Path.Combine(
                        directory.FullName,
                        "pkg.json");

                    if (!File.Exists(manifestPath))
                    {
                        continue;
                    }

                    using System.Text.Json.JsonDocument document =
                        System.Text.Json.JsonDocument.Parse(
                            File.ReadAllText(manifestPath));

                    System.Text.Json.JsonElement root =
                        document.RootElement;

                    return new PackageManifest(
                        GetJsonString(root, "version"),
                        GetJsonString(root, "description"),
                        GetJsonString(root, "license"),
                        GetJsonString(root, "repository_url"),
                        GetJsonString(root, "site_url"));
                }
            }
            catch
            {
            }

            return new PackageManifest();
        }
        private static string? GetJsonString(
            System.Text.Json.JsonElement root,
            string propertyName)
        {
            return root.TryGetProperty(
                    propertyName,
                    out System.Text.Json.JsonElement value)
                && value.ValueKind ==
                    System.Text.Json.JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        internal static (
        string HostName,
        string? HostVersion)
        GetHostApplication()
        {
            try
            {
                using Process process =
                    Process.GetCurrentProcess();


                return (
                    process.ProcessName,
                    process
                        .MainModule?
                        .FileVersionInfo
                        .ProductVersion);
            }
            catch
            {
                return (
                    "unknown",
                    null);
            }
        }
        private sealed record PackageManifest(
        string? Version = null,
        string? Description = null,
        string? License = null,
        string? RepositoryUrl = null,
        string? SiteUrl = null);
    }
}
