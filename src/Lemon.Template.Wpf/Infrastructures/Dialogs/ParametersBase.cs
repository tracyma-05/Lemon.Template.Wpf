// Ported from Prism's parameter bag. Its contract deliberately hands back `default` for a missing or
// type-mismatched key, which nullable reference analysis cannot express through the generic signatures
// without either changing that contract or littering the file with `!`. Quarantined here on purpose, and
// visible in the file rather than hidden in a NoWarn list.
// TODO: revisit alongside a first-party rewrite of the dialog parameter API.
#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Lemon.Template.Wpf.Infrastructures.Dialogs
{
    public abstract class ParametersBase : IParameters, IEnumerable<KeyValuePair<string, object>>, IEnumerable
    {
        private readonly List<KeyValuePair<string, object>> _entries = new List<KeyValuePair<string, object>>();

        //
        // Summary:
        //     Searches Parameter collection and returns value if Collection contains key. Otherwise
        //     returns null.
        //
        // Parameters:
        //   key:
        //     The key for the value to be returned.
        //
        // Returns:
        //     The value of the parameter referenced by the key; otherwise null.
        public object this[string key]
        {
            get
            {
                foreach (KeyValuePair<string, object> entry in _entries)
                {
                    if (string.Compare(entry.Key, key, StringComparison.Ordinal) == 0)
                    {
                        return entry.Value;
                    }
                }

                return null;
            }
        }

        //
        // Summary:
        //     The count, or number, of parameters in collection.
        public int Count => _entries.Count;

        //
        // Summary:
        //     Returns an IEnumerable of the Keys in the collection.
        public IEnumerable<string> Keys => _entries.Select((KeyValuePair<string, object> x) => x.Key);

        //
        // Summary:
        //     Default constructor.
        protected ParametersBase()
        {
        }

        //
        // Summary:
        //     Constructs a list of parameters.
        //
        // Parameters:
        //   query:
        //     Query string to be parsed.
        protected ParametersBase(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            int length = query.Length;
            for (int i = ((query.Length > 0 && query[0] == '?') ? 1 : 0); i < length; i++)
            {
                int num = i;
                int num2 = -1;
                for (; i < length; i++)
                {
                    switch (query[i])
                    {
                        case '=':
                            if (num2 < 0)
                            {
                                num2 = i;
                            }

                            continue;
                        default:
                            continue;
                        case '&':
                            break;
                    }

                    break;
                }

                string text = null;
                string stringToUnescape;
                if (num2 >= 0)
                {
                    text = query.Substring(num, num2 - num);
                    stringToUnescape = query.Substring(num2 + 1, i - num2 - 1);
                }
                else
                {
                    stringToUnescape = query.Substring(num, i - num);
                }

                if (text != null)
                {
                    Add(Uri.UnescapeDataString(text), Uri.UnescapeDataString(stringToUnescape));
                }
            }
        }

        //
        // Summary:
        //     Adds the key and value to the parameters collection.
        //
        // Parameters:
        //   key:
        //     The key to reference this value in the parameters collection.
        //
        //   value:
        //     The value of the parameter to store.
        public void Add(string key, object value)
        {
            _entries.Add(new KeyValuePair<string, object>(key, value));
        }

        //
        // Summary:
        //     Checks collection for presence of key.
        //
        // Parameters:
        //   key:
        //     The key to check in the collection.
        //
        // Returns:
        //     true if key exists; else returns false.
        public bool ContainsKey(string key)
        {
            return _entries.ContainsKey(key);
        }

        //
        // Summary:
        //     Gets an enumerator for the KeyValuePairs in parameter collection.
        //
        // Returns:
        //     Enumerator.
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _entries.GetEnumerator();
        }

        //
        // Summary:
        //     Returns the value of the member referenced by key.
        //
        // Parameters:
        //   key:
        //     The key for the value to be returned.
        //
        // Type parameters:
        //   T:
        //     The type of object to be returned.
        //
        // Returns:
        //     Returns a matching parameter of T if one exists in the Collection.
        public T GetValue<T>(string key)
        {
            return _entries.GetValue<T>(key);
        }

        //
        // Summary:
        //     Returns an IEnumerable of all parameters.
        //
        // Parameters:
        //   key:
        //     The key for the values to be returned.
        //
        // Type parameters:
        //   T:
        //     The type for the values to be returned.
        //
        // Returns:
        //     Returns a IEnumerable of all the instances of type T.
        public IEnumerable<T> GetValues<T>(string key)
        {
            return _entries.GetValues<T>(key);
        }

        //
        // Summary:
        //     Checks to see if the parameter collection contains the value.
        //
        // Parameters:
        //   key:
        //     The key for the value to be returned.
        //
        //   value:
        //     Value of the returned parameter if it exists.
        //
        // Type parameters:
        //   T:
        //     The type for the values to be returned.
        public bool TryGetValue<T>(string key, out T value)
        {
            return _entries.TryGetValue<T>(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        //
        // Summary:
        //     Converts parameter collection to a parameter string.
        //
        // Returns:
        //     A string representation of the parameters.
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (_entries.Count > 0)
            {
                stringBuilder.Append('?');
                bool flag = true;
                foreach (KeyValuePair<string, object> entry in _entries)
                {
                    if (!flag)
                    {
                        stringBuilder.Append('&');
                    }
                    else
                    {
                        flag = false;
                    }

                    stringBuilder.Append(Uri.EscapeDataString(entry.Key));
                    stringBuilder.Append('=');
                    stringBuilder.Append(Uri.EscapeDataString((entry.Value != null) ? entry.Value.ToString() : ""));
                }
            }

            return stringBuilder.ToString();
        }

        //
        // Summary:
        //     Adds a collection of parameters to the local parameter list.
        //
        // Parameters:
        //   parameters:
        //     An IEnumerable of KeyValuePairs to add to the current parameter list.
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void FromParameters(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            _entries.AddRange(parameters);
        }
    }
}