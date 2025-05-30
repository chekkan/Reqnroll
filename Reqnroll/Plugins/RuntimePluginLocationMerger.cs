using System;
using System.Collections.Generic;
using System.IO;

namespace Reqnroll.Plugins
{
    public class RuntimePluginLocationMerger : IRuntimePluginLocationMerger
    {
        public IReadOnlyList<string> Merge(IReadOnlyList<string> pluginPaths)
        {
            // Idea is to filter out the same assemblies stored on different paths. Shortcut: check if we even have duplicated assemblies
            var hashset = new HashSet<string>();

            List<string> modifiedList = null;
            for (var i = 0; i < pluginPaths.Count; i++)
            {
                if (hashset.Add(Path.GetFileName(pluginPaths[i])))
                {
                    // Assembly not duplicated
                    if (modifiedList is null)
                    {
                        // No duplication yet, continue
                        continue;
                    }

                    // We already had a duplication, fill the list with this non duplicated entry
                    modifiedList.Add(pluginPaths[i]);
                }
                else if (modifiedList is null)
                {
                    // First duplicated assembly, Copy all previous entry into the new list
                    modifiedList = CopyUntilIndex(pluginPaths, i);
                }
            }

            return SortPluginPaths(modifiedList ?? [.. pluginPaths]);
        }

        private static IReadOnlyList<string> SortPluginPaths(List<string> pluginPaths)
        {
            pluginPaths.Sort((x, y) => string.Compare(Path.GetFileName(x), Path.GetFileName(y), StringComparison.OrdinalIgnoreCase));
            return pluginPaths;
        }

        private static List<string> CopyUntilIndex(IReadOnlyList<string> pluginPaths, int index)
        {
            var result = new List<string>(pluginPaths.Count);
            for (var i = 0; i < index; i++)
            {
                result.Add(pluginPaths[i]);
            }
            return result;
        }
    }
}