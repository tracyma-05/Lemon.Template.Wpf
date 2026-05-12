using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Lemon.Template.Wpf.Infrastructures.Navigations
{
    public class NavigationParameters : Dictionary<string, object?>
    {
        public T GetValue<T>(string key)
        {
            if (TryGetValue(key, out var value) && value is T t)
            {
                return t;
            }

            return default!;
        }

        public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        {
            if (TryGetValue(key, out var boxed) && boxed is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void AddRange(IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                this[pair.Key] = pair.Value;
            }
        }
    }
}