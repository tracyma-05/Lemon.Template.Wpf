using System.Collections.Generic;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public interface IDialogParameters
    {
        //
        // Summary:
        //     The number of parameters in the collection.
        int Count { get; }

        //
        // Summary:
        //     The keys in the collection.
        IEnumerable<string> Keys { get; }

        //
        // Summary:
        //     Adds the key and value to the collection.
        //
        // Parameters:
        //   key:
        //     The key to reference this parameter value in the collection.
        //
        //   value:
        //     The parameter value to store.
        void Add(string key, object value);

        //
        // Summary:
        //     Checks the collection for the presence of a key.
        //
        // Parameters:
        //   key:
        //     The key to check.
        //
        // Returns:
        //     true if key exists; false otherwise.
        bool ContainsKey(string key);

        //
        // Summary:
        //     Gets the parameter value referenced by a key.
        //
        // Parameters:
        //   key:
        //     The key of the parameter value to be returned.
        //
        // Type parameters:
        //   T:
        //     The type of object to be returned.
        //
        // Returns:
        //     The matching parameter of type T.
        T GetValue<T>(string key);

        //
        // Summary:
        //     Gets all parameter values referenced by a key.
        //
        // Parameters:
        //   key:
        //     The key of the parameter values to be returned.
        //
        // Type parameters:
        //   T:
        //     The type of object to be returned.
        //
        // Returns:
        //     All matching parameter values of type T.
        IEnumerable<T> GetValues<T>(string key);

        //
        // Summary:
        //     Gets the parameter value if the referenced key exists.
        //
        // Parameters:
        //   key:
        //     The key of the parameter value to be returned.
        //
        //   value:
        //     The matching parameter of type T if the key exists.
        //
        // Type parameters:
        //   T:
        //     The type of object to be returned.
        //
        // Returns:
        //     true if the parameter exists; false otherwise.
        bool TryGetValue<T>(string key, out T value);
    }
}