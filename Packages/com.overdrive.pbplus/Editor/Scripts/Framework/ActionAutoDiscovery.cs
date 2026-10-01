using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Overdrive.ProBuilderPlus
{
    /// <summary>
    /// Scans assemblies for actions with ProBuilderPlusActionAttributes.
    /// </summary>
    public static class ActionAutoDiscovery
    {
        /// <summary>
        /// Static collection of term that an assembly name starts with that are
        /// not containing <see cref="ProBuilderPlusActionAttribute"/> so they do not need to be checked.
        /// </summary>
        private static readonly string[] nonCheckAssemblies = new string[]
        {
            "Unity",
            "System",
            "Mono.",
            "mscorlib",
            "netstandard",
            "Newtonsoft",
            "nunit.framework",
            "Anonymously Hosted DynamicMethods Assembly",
            "Bee.BeeDriver2",
            "Bee.BinLog",
            "Bee.TinyProfiler2",
            "Domain_Reload",
            "ExCSS.Unity",
            "I18N",
            "I18N.West",
            "PlayerBuildProgramLibrary.Data",
            "PPv2URPConverters",
            "ScriptCompilationBuildProgram.Data",
            "WinPlayerBuildProgram.Data",
        };

        /// <summary>
        /// Creates a list of (Type,ProBuilderPlusActionAttribute) pairs from reflection
        /// on all types of all loaded assemblies that ae not part of the exclusion-list.
        /// </summary>
        public static List<(Type Type, ProBuilderPlusActionAttribute ActionAttribute)> DiscoverActions()
        {
            var attributes = new List<(Type, ProBuilderPlusActionAttribute)>();

            // Get all loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (IsNoAttributesAssembly(assembly.FullName))
                {
                    continue;
                }

                try
                {
                    var candidateTypes = assembly.GetTypes().Where(static t => !t.IsAbstract && !t.IsInterface);
                    foreach (var candidateType in candidateTypes)
                    {
                        var attribute = candidateType.GetCustomAttribute<ProBuilderPlusActionAttribute>();
                        if (attribute != null)
                        {
                            attributes.Add((candidateType, attribute));
                        }
                    }
                }
                catch (Exception e)
                {
                    // Skip assemblies that can't be reflected (like native assemblies)
                    Debug.LogWarning($"Could not scan assembly {assembly.FullName} for ProBuilderPlus actions: {e.Message}");
                }
            }

            return attributes;
        }

        /// <summary>
        /// Checks if a the given assembly name is in the list of assemblies that will not be checked for <see cref="ProBuilderPlusActionAttribute"/>.
        /// </summary>
        /// <param name="assemblyFullName">Fully qualified assembly name.</param>
        /// <returns>True if the assembly is to be skipped.</returns>
        private static bool IsNoAttributesAssembly(string assemblyFullName)
        {
            for (int index = 0; index < nonCheckAssemblies.Length; index++)
            {
                if (assemblyFullName.StartsWith(nonCheckAssemblies[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
