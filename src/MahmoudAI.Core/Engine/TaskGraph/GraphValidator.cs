using System;
using System.Collections.Generic;
using System.Linq;

namespace MahmoudAI.Core.Engine.TaskGraph
{
    public static class GraphValidator
    {
        public static void Validate(IEnumerable<MissionTaskDefinition> tasks)
        {
            var list = tasks.ToList();
            var ids = new HashSet<string>();

            foreach (var task in list)
            {
                if (string.IsNullOrWhiteSpace(task.Id))
                {
                    throw new ArgumentException("Task ID cannot be null or whitespace.");
                }
                if (!ids.Add(task.Id))
                {
                    throw new InvalidOperationException($"Duplicate task ID detected: '{task.Id}'.");
                }
            }

            foreach (var task in list)
            {
                foreach (var dep in task.Dependencies)
                {
                    if (!ids.Contains(dep))
                    {
                        throw new InvalidOperationException($"Task '{task.Id}' depends on missing task ID '{dep}'.");
                    }
                }
            }

            // Cycle detection using DFS color marking (0 = unvisited, 1 = visiting, 2 = visited)
            var state = new Dictionary<string, int>();
            foreach (var task in list)
            {
                state[task.Id] = 0;
            }

            var adj = list.ToDictionary(t => t.Id, t => t.Dependencies);

            bool HasCycle(string u)
            {
                state[u] = 1;
                foreach (var v in adj[u])
                {
                    if (state[v] == 1) return true;
                    if (state[v] == 0 && HasCycle(v)) return true;
                }
                state[u] = 2;
                return false;
            }

            foreach (var task in list)
            {
                if (state[task.Id] == 0)
                {
                    if (HasCycle(task.Id))
                    {
                        throw new InvalidOperationException($"Circular dependency detected in graph involving task '{task.Id}'.");
                    }
                }
            }
        }
    }
}
