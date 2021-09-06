//-----------------------------------------------------------------------
// <copyright file="SimpleJson.cs" company="The Outercurve Foundation">
//    Copyright (c) 2011, The Outercurve Foundation.
//    Copyright (c) 2012, Antmicro <www.antmicro.com>
//
//    Licensed under the MIT License (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.opensource.org/licenses/mit-license.php
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>
// <author>Nathan Totten (ntotten.com), Jim Zimmerman (jimzimmerman.com) and Prabir Shrestha (prabir.me), Piotr Zierhoffer (antmicro.com)</author>
// <website>https://github.com/facebook-csharp-sdk/simple-json</website>
//-----------------------------------------------------------------------

// NOTE: uncomment the following line to make SimpleJson class internal.
#define SIMPLE_JSON_INTERNAL

// NOTE: uncomment the following line to make JsonArray and JsonObject class internal.
#define SIMPLE_JSON_OBJARRAYINTERNAL

// NOTE: uncomment the following line to enable dynamic support.
#define SIMPLE_JSON_DYNAMIC

// original json parsing code from http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Antmicro.Renode.Config.Reflection;

namespace Antmicro.Renode.Config
{
    #region JsonArray

    /// <summary>
    /// Represents the json array.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]

    public class JsonArray : List<object>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArray"/> class. 
        /// </summary>
        public JsonArray()
        {
        }

        /// <summary>
        /// Returns the array of T if every element is T, array of object otherwise. 
        /// </summary>
        /// <returns>
        /// This array, but strongly typed if possible.
        /// </returns>
        public dynamic ToDynamic()
        {
            if(this.Count > 0)
            {
                return Dynamize((dynamic)this[0]);
            }
            return this;
        }

        private dynamic Dynamize<T>(T elem)
        {
            if(this.All(x => x is T))
            {
                return new List<T>(this.Cast<T>());
            }
            return this;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArray"/> class. 
        /// </summary>
        /// <param name="capacity">The capacity of the json array.</param>
        public JsonArray(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// The json representation of the array.
        /// </summary>
        /// <returns>The json representation of the array.</returns>
        public override string ToString()
        {
            return SimpleJson.SerializeObject(this) ?? string.Empty;
        }
    }

    #endregion

    #region JsonObject

    internal static class HashSetExtensions
    {
        public static void Add<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key, object value)
        {
            hashSet.Add(new KeyValuePair<K, object>(key, value));
        }

        public static bool ContainsKey<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key)
        {
            return hashSet.Any(x => x.Key.Equals(key));
        }

        public static ICollection<K> Keys<K>(this HashSet<KeyValuePair<K, object>> hashSet)
        {
            return hashSet.Select(x => x.Key).ToList();
        }

        public static bool Remove<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key)
        {
            return hashSet.RemoveWhere(x => x.Key.Equals(key)) > 0;
        }

        public static bool RemoveFirst<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key)
        {
            var found = false;
            return hashSet.RemoveWhere(x => {if(!found && x.Key.Equals(key)) {found = true; return true;} return false;}) > 0;
        }

        public static bool TryGetValue<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key, out object value)
        {
            var valueEntry = hashSet.FirstOrDefault(x => x.Key.Equals(key));
            if(valueEntry.Equals(default(KeyValuePair<K, object>)))
            {
                value = valueEntry.Value;
                return true;
            }
            value = null;
            return false;
        }

        public static object Get<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key)
        {
            return hashSet.First(x => x.Key.Equals(key)).Value;
        }

        public static void Set<K>(this HashSet<KeyValuePair<K, object>> hashSet, K key, object value)
        {
            hashSet.RemoveFirst(key);
            hashSet.Add(key, value);
        }

        public static ICollection<object> Values<K>(this HashSet<KeyValuePair<K, object>> hashSet)
        {
            return hashSet.Select(x => x.Value).ToList();
        }
    }

    /// <summary>
    /// Represents the json object.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    internal class JsonObject : DynamicObject, IDictionary<string, object>
    {
        /// <summary>
        /// The internal member dictionary.
        /// </summary>
        private readonly HashSet<KeyValuePair<string, object>> _members = new HashSet<KeyValuePair<string, object>>();

        /// <summary>
        /// Gets the <see cref="System.Object"/> at the specified index.
        /// </summary>
        /// <value></value>
        public object this[int index]
        {
            get { return GetAtIndex(_members, index); }
        }

        internal static object GetAtIndex(HashSet<KeyValuePair<string, object>> obj, int index)
        {
            if(obj == null)
            {
                throw new ArgumentNullException("obj");
            }

            if(index >= obj.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            int i = 0;
            foreach(KeyValuePair<string, object> o in obj)
                if(i++ == index)
                {
                    return o.Value;
                }

            return null;
        }

        /// <summary>
        /// Adds the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Add(string key, object value)
        {
            _members.Add(key, value);
        }

        /// <summary>
        /// Determines whether the specified key contains key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        ///     <c>true</c> if the specified key contains key; otherwise, <c>false</c>.
        /// </returns>
        public bool ContainsKey(string key)
        {
            return _members.ContainsKey(key);
        }

        /// <summary>
        /// Gets the keys.
        /// </summary>
        /// <value>The keys.</value>
        public ICollection<string> Keys
        {
            get { return _members.Keys(); }
        }

        /// <summary>
        /// Removes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            return _members.Remove(key);
        }

        /// <summary>
        /// Tries the get value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public bool TryGetValue(string key, out object value)
        {
            return _members.TryGetValue(key, out value);
        }

        /// <summary>
        /// Gets the values.
        /// </summary>
        /// <value>The values.</value>
        public ICollection<object> Values
        {
            get { return _members.Values(); }
        }

        /// <summary>
        /// Gets or sets the <see cref="System.Object"/> with the specified key.
        /// </summary>
        /// <value></value>
        public object this[string key]
        {
            get { return _members.Get(key); }
            set { _members.Set(key, value); }
        }

        /// <summary>
        /// Adds the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        public void Add(KeyValuePair<string, object> item)
        {
            _members.Add(item.Key, item.Value);
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        public void Clear()
        {
            _members.Clear();
        }

        /// <summary>
        /// Determines whether [contains] [the specified item].
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>
        ///     <c>true</c> if [contains] [the specified item]; otherwise, <c>false</c>.
        /// </returns>
        public bool Contains(KeyValuePair<string, object> item)
        {
            return _members.ContainsKey(item.Key) && _members.Get(item.Key) == item.Value;
        }

        /// <summary>
        /// Copies to.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="arrayIndex">Index of the array.</param>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            int num = Count;
            foreach(KeyValuePair<string, object> kvp in this)
            {
                array[arrayIndex++] = kvp;

                if(--num <= 0)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>The count.</value>
        public int Count
        {
            get { return _members.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is read only.
        /// </summary>
        /// <value>
        ///     <c>true</c> if this instance is read only; otherwise, <c>false</c>.
        /// </value>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Removes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public bool Remove(KeyValuePair<string, object> item)
        {
            return _members.Remove(item.Key);
        }

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _members.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _members.GetEnumerator();
        }

        /// <summary>
        /// Returns a json <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A json <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return SimpleJson.SerializeObject(this);
        }

        /// <summary>
        /// Provides implementation for type conversion operations. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations that convert an object from one type to another.
        /// </summary>
        /// <param name="binder">Provides information about the conversion operation. The binder.Type property provides the type to which the object must be converted. For example, for the statement (String)sampleObject in C# (CType(sampleObject, Type) in Visual Basic), where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, binder.Type returns the <see cref="T:System.String"/> type. The binder.Explicit property provides information about the kind of conversion that occurs. It returns true for explicit conversion and false for implicit conversion.</param>
        /// <param name="result">The result of the type conversion operation.</param>
        /// <returns>
        /// Alwasy returns true.
        /// </returns>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            // <pex>
            if(binder == (ConvertBinder)null)
            {
                throw new ArgumentNullException("binder");
            }
            // </pex>
            Type targetType = binder.Type;

            if((targetType == typeof(IEnumerable)) ||
                (targetType == typeof(IEnumerable<KeyValuePair<string, object>>)) ||
                (targetType == typeof(IDictionary<string, object>)) ||
                (targetType == typeof(IDictionary)))
            {
                result = this;
                return true;
            }

            return base.TryConvert(binder, out result);
        }

        /// <summary>
        /// Provides the implementation for operations that delete an object member. This method is not intended for use in C# or Visual Basic.
        /// </summary>
        /// <param name="binder">Provides information about the deletion.</param>
        /// <returns>
        /// Alwasy returns true.
        /// </returns>
        public override bool TryDeleteMember(DeleteMemberBinder binder)
        {
            // <pex>
            if(binder == (DeleteMemberBinder)null)
            {
                throw new ArgumentNullException("binder");
            }
            // </pex>
            return _members.Remove(binder.Name);
        }

        /// <summary>
        /// Provides the implementation for operations that get a value by index. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for indexing operations.
        /// </summary>
        /// <param name="binder">Provides information about the operation.</param>
        /// <param name="indexes">The indexes that are used in the operation. For example, for the sampleObject[3] operation in C# (sampleObject(3) in Visual Basic), where sampleObject is derived from the DynamicObject class, <paramref name="indexes"/> is equal to 3.</param>
        /// <param name="result">The result of the index operation.</param>
        /// <returns>
        /// Alwasy returns true.
        /// </returns>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if(indexes.Length == 1)
            {
                result = ((IDictionary<string, object>)this)[(string)indexes[0]];
                return true;
            }
            result = (object)null;
            return true;
        }

        /// <summary>
        /// Provides the implementation for operations that get member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations such as getting a value for a property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member on which the dynamic operation is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty) statement, where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="result">The result of the get operation. For example, if the method is called for a property, you can assign the property value to <paramref name="result"/>.</param>
        /// <returns>
        /// Alwasy returns true.
        /// </returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            object value;
            if(_members.TryGetValue(binder.Name, out value))
            {
                result = value;
                return true;
            }
            result = (object)null;
            return true;
        }

        /// <summary>
        /// Provides the implementation for operations that set a value by index. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations that access objects by a specified index.
        /// </summary>
        /// <param name="binder">Provides information about the operation.</param>
        /// <param name="indexes">The indexes that are used in the operation. For example, for the sampleObject[3] = 10 operation in C# (sampleObject(3) = 10 in Visual Basic), where sampleObject is derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, <paramref name="indexes"/> is equal to 3.</param>
        /// <param name="value">The value to set to the object that has the specified index. For example, for the sampleObject[3] = 10 operation in C# (sampleObject(3) = 10 in Visual Basic), where sampleObject is derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, <paramref name="value"/> is equal to 10.</param>
        /// <returns>
        /// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown.
        /// </returns>
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if(indexes.Length == 1)
            {
                ((IDictionary<string, object>)this)[(string)indexes[0]] = value;
                return true;
            }

            return base.TrySetIndex(binder, indexes, value);
        }

        /// <summary>
        /// Provides the implementation for operations that set member values. Classes derived from the <see cref="T:System.Dynamic.DynamicObject"/> class can override this method to specify dynamic behavior for operations such as setting a value for a property.
        /// </summary>
        /// <param name="binder">Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member to which the value is being assigned. For example, for the statement sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive.</param>
        /// <param name="value">The value to set to the member. For example, for sampleObject.SampleProperty = "Test", where sampleObject is an instance of the class derived from the <see cref="T:System.Dynamic.DynamicObject"/> class, the <paramref name="value"/> is "Test".</param>
        /// <returns>
        /// true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown.)
        /// </returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            // <pex>
            if(binder == (SetMemberBinder)null)
            {
                throw new ArgumentNullException("binder");
            }
            // </pex>
            _members.Set(binder.Name, value);
            return true;
        }

        /// <summary>
        /// Returns the enumeration of all dynamic member names.
        /// </summary>
        /// <returns>
        /// A sequence that contains dynamic member names.
        /// </returns>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            foreach(var key in Keys)
                yield return key;
        }
    }

    #endregion
}

namespace Antmicro.Renode.Config
{
    #region JsonParser

    /// <summary>
    /// This class encodes and decodes JSON strings.
    /// Spec. details, see http://www.json.org/
    /// 
    /// JSON uses Arrays and Objects. These correspond here to the datatypes JsonArray(IList&lt;object>) and JsonObject(IDictionary&lt;string,object>).
    /// All numbers are parsed to doubles.
    /// </summary>
    public class SimpleJson
    {
        private const int TOKEN_NONE = 0;
        private const int TOKEN_CURLY_OPEN = 1;
        private const int TOKEN_CURLY_CLOSE = 2;
        private const int TOKEN_SQUARED_OPEN = 3;
        private const int TOKEN_SQUARED_CLOSE = 4;
        private const int TOKEN_COLON = 5;
        private const int TOKEN_COMMA = 6;
        private const int TOKEN_STRING = 7;
        private const int TOKEN_NUMBER = 8;
        private const int TOKEN_TRUE = 9;
        private const int TOKEN_FALSE = 10;
        private const int TOKEN_NULL = 11;
        private const int BUILDER_CAPACITY = 2000;

        /// <summary>
        /// Parses the string json into a value
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <returns>An IList&lt;object>, a IDictionary&lt;string,object>, a double, a string, null, true, or false</returns>
        public static object DeserializeObject (string json)
		{
			object @object;
			int line = 1;
            if(TryDeserializeObject(json, out @object, ref line))
            {
                return @object;
            }
			var numOfLines = json.Count (x=> x == '\n');
			if(line > numOfLines)
			{
				throw new System.Runtime.Serialization.SerializationException(String.Format("Invalid JSON string. Probably a closing brace is missing."));
			}
			throw new System.Runtime.Serialization.SerializationException(String.Format("Invalid JSON string on line {0}.", line));
        }

        /// <summary>
        /// Try parsing the json string into a value.
        /// </summary>
        /// <param name="json">
        /// A JSON string.
        /// </param>
        /// <param name="object">
        /// The object.
        /// </param>
        /// <returns>
        /// Returns true if successfull otherwise false.
        /// </returns>
        public static bool TryDeserializeObject(string json, out object @object, ref int line)
        {
            bool success = true;
            if(json != null)
            {
                char[] charArray = json.ToCharArray();
                int index = 0;
                @object = ParseValue(charArray, ref index, ref success, ref line);
                EatWhitespace(charArray, ref index, ref line);
                EatComment(charArray, ref index, ref line);
                if(index != charArray.Length)
                {
                    //json ends too quickly
                    success = false;
                }
            }
            else
            {
                 @object = null;
            }

            return success;
        }

        public static object DeserializeObject(string json, Type type, IJsonSerializerStrategy jsonSerializerStrategy)
        {
            object jsonObject = DeserializeObject(json);

            return type == null || jsonObject != null &&

                jsonObject.GetType().IsAssignableFrom(type) ? jsonObject
                       : (jsonSerializerStrategy ?? CurrentJsonSerializerStrategy).DeserializeObject(jsonObject, type);
        }

        public static object DeserializeObject(string json, Type type)
        {
            return DeserializeObject(json, type, null);
        }

        public static T DeserializeObject<T>(string json, IJsonSerializerStrategy jsonSerializerStrategy)
        {
            return (T)DeserializeObject(json, typeof(T), jsonSerializerStrategy);
        }

        public static T DeserializeObject<T>(string json)
        {
            return (T)DeserializeObject(json, typeof(T), null);
        }

        /// <summary>
        /// Converts a IDictionary&lt;string,object> / IList&lt;object> object into a JSON string
        /// </summary>
        /// <param name="json">A IDictionary&lt;string,object> / IList&lt;object></param>
        /// <param name="jsonSerializerStrategy">Serializer strategy to use</param>
        /// <returns>A JSON encoded string, or null if object 'json' is not serializable</returns>
        public static string SerializeObject(object json, IJsonSerializerStrategy jsonSerializerStrategy)
        {
            StringBuilder builder = new StringBuilder(BUILDER_CAPACITY);
            bool success = SerializeValue(jsonSerializerStrategy, json, builder);
            return (success ? builder.ToString() : null);
        }

        public static string SerializeObject(object json)
        {
            return SerializeObject(json, CurrentJsonSerializerStrategy);
        }

        public static string EscapeToJavascriptString(string jsonString)
        {
            if(string.IsNullOrEmpty(jsonString))
            {
                return jsonString;
            }

            StringBuilder sb = new StringBuilder();
            char c;

            for(int i = 0; i < jsonString.Length;)
            {
                c = jsonString[i++];

                if(c == '\\')
                {
                    int remainingLength = jsonString.Length - i;
                    if(remainingLength >= 2)
                    {
                        char lookahead = jsonString[i];
                        if(lookahead == '\\')
                        {
                            sb.Append('\\');
                            ++i;
                        }
                        else
                        if(lookahead == '"')
                        {
                            sb.Append("\"");
                            ++i;
                        }
                        else
                        if(lookahead == 't')
                        {
                            sb.Append('\t');
                            ++i;
                        }
                        else
                        if(lookahead == 'b')
                        {
                            sb.Append('\b');
                            ++i;
                        }
                        else
                        if(lookahead == 'n')
                        {
                            sb.Append('\n');
                            ++i;
                        }
                        else
                        if(lookahead == 'r')
                        {
                            sb.Append('\r');
                            ++i;
                        }
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        protected static IDictionary<string, object> ParseObject(char[] json, ref int index, ref bool success, ref int line)
        {
            IDictionary<string, object> table = new JsonObject();
            int token;

            // {
            NextToken(json, ref index, ref line);

            bool done = false;
            while(!done)
            {
                token = LookAhead(json, index, line);
                if(token == TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else
                if(token == TOKEN_COMMA)
                {
                    NextToken(json, ref index, ref line);
                }
                else
                if(token == TOKEN_CURLY_CLOSE)
                {
                    NextToken(json, ref index, ref line);
                    return table;
                }
                else
                {
                    // name
                    string name = ParseString(json, ref index, ref success, ref line);
                    if(!success)
                    {
                        return null;
                    }

                    // :
                    token = NextToken(json, ref index, ref line);
                    if(token != TOKEN_COLON)
                    {
                        success = false;
                        return null;
                    }

                    // value
                    object value = ParseValue(json, ref index, ref success, ref line);
                    if(!success)
                    {
                        return null;
                    }

                    int nextToken = LookAhead(json, index, line);
                    if(nextToken != TOKEN_COMMA && nextToken != TOKEN_CURLY_CLOSE)
                    {
                        success = false;
                        return null;
                    }

                    table.Add(name, value);
                }
            }

            return table;
        }

        protected static JsonArray ParseArray(char[] json, ref int index, ref bool success, ref int line)
        {
            JsonArray array = new JsonArray();

            // [
            NextToken(json, ref index, ref line);

            bool done = false;
            while(!done)
            {
                int token = LookAhead(json, index, line);
                if(token == TOKEN_NONE)
                {
                    success = false;
                    return null;
                }
                else
                if(token == TOKEN_COMMA)
                {
                    NextToken(json, ref index, ref line);
                }
                else
                if(token == TOKEN_SQUARED_CLOSE)
                {
                    NextToken(json, ref index, ref line);
                    break;
                }
                else
                {
                    object value = ParseValue(json, ref index, ref success, ref line);
                    if(!success)
                    {
                        return null;
                    }

                    int nextToken = LookAhead(json, index, line);
                    if(nextToken != TOKEN_COMMA && nextToken != TOKEN_SQUARED_CLOSE)
                    {
                        return null;
                    }

                    array.Add(value);
                }
            }

            return array;
        }

        protected static object ParseValue(char[] json, ref int index, ref bool success, ref int line)
        {
            switch(LookAhead(json, index, line))
            {
            case TOKEN_STRING:
                return ParseString(json, ref index, ref success, ref line);
            case TOKEN_NUMBER:
                return ParseNumber(json, ref index, ref success, ref line);
            case TOKEN_CURLY_OPEN:
                return ParseObject(json, ref index, ref success, ref line);
            case TOKEN_SQUARED_OPEN:
                return ParseArray(json, ref index, ref success, ref line);
            case TOKEN_TRUE:
                NextToken(json, ref index, ref line);
                return true;
            case TOKEN_FALSE:
                NextToken(json, ref index, ref line);
                return false;
            case TOKEN_NULL:
                NextToken(json, ref index, ref line);
                return null;
            case TOKEN_NONE:
                break;
            }

            success = false;
            return null;
        }

        protected static string ParseString(char[] json, ref int index, ref bool success, ref int line)
        {
            StringBuilder s = new StringBuilder(BUILDER_CAPACITY);
            char c;

            EatWhitespace(json, ref index, ref line);
            EatComment(json, ref index, ref line);

            // "s
            c = json[index++];

            bool complete = false;
            while(!complete)
            {
                if(index == json.Length)
                {
                    break;
                }

                c = json[index++];
                if(c == '"')
                {
                    complete = true;
                    break;
                }
                else
                if(c == '\\')
                {
                    if(index == json.Length)
                    {
                        break;
                    }
                    c = json[index++];
                    if(c == '"')
                    {
                        s.Append('"');
                    }
                    else
                    if(c == '\\')
                    {
                        s.Append('\\');
                    }
                    else
                    if(c == '/')
                    {
                        s.Append('/');
                    }
                    else
                    if(c == 'b')
                    {
                        s.Append('\b');
                    }
                    else
                    if(c == 'f')
                    {
                        s.Append('\f');
                    }
                    else
                    if(c == 'n')
                    {
                        s.Append('\n');
                    }
                    else
                    if(c == 'r')
                    {
                        s.Append('\r');
                    }
                    else
                    if(c == 't')
                    {
                        s.Append('\t');
                    }
                    else
                    if(c == 'u')
                    {
                        int remainingLength = json.Length - index;
                        if(remainingLength >= 4)
                        {
                            // parse the 32 bit hex into an integer codepoint
                            uint codePoint;
                            if(
                                !(success =
                                  UInt32.TryParse(new string(json, index, 4), NumberStyles.HexNumber,
                                                  CultureInfo.InvariantCulture, out codePoint)))
                            {
                                return "";
                            }

                            // convert the integer codepoint to a unicode char and add to string

                            if(0xD800 <= codePoint && codePoint <= 0xDBFF)  // if high surrogate
                            {
                                index += 4; // skip 4 chars
                                remainingLength = json.Length - index;
                                if(remainingLength >= 6)
                                {
                                    uint lowCodePoint;
                                    if(new string(json, index, 2) == "\\u" &&
                                        UInt32.TryParse(new string(json, index + 2, 4), NumberStyles.HexNumber,
                                                        CultureInfo.InvariantCulture, out lowCodePoint))
                                    {
                                        if(0xDC00 <= lowCodePoint && lowCodePoint <= 0xDFFF)    // if low surrogate
                                        {
                                            s.Append((char)codePoint);
                                            s.Append((char)lowCodePoint);
                                            index += 6; // skip 6 chars
                                            continue;
                                        }
                                    }
                                }
                                success = false;    // invalid surrogate pair
                                return "";
                            }
                            s.Append(Char.ConvertFromUtf32((int)codePoint));
                            // skip 4 chars
                            index += 4;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    s.Append(c);
                }
            }

            if(!complete)
            {
                success = false;
                return null;
            }

            return s.ToString();
        }

        private static char[] HexArray = "aAbBcCdDeEfFxX".ToCharArray();

        protected static object ParseNumber(char[] json, ref int index, ref bool success, ref int line)
        {
            EatWhitespace(json, ref index, ref line);
            EatComment(json, ref index, ref line);

            int lastIndex = GetLastIndexOfNumber(json, index);
            int charLength = (lastIndex - index) + 1;

            object returnNumber;
            string str = new string(json, index, charLength);
            if(str.StartsWith("0x"))
            {
                long number;
                
                success = long.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out number);
                if(number <= int.MaxValue && number >= int.MinValue)
                {
                    returnNumber = (int)number;
                }
                else
                {
                    returnNumber = number;
                }
            }
            else
            if(str.IndexOfAny(HexArray) != -1)
            {
                success = false;
                returnNumber = 0;
            }
            else
            {
                if(str.IndexOf(".", StringComparison.OrdinalIgnoreCase) != -1 || str.IndexOf("e", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    double number;
                    success = double.TryParse(new string(json, index, charLength), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
                    returnNumber = number;
                }
                else
                {
                    long number;
                    success = long.TryParse(new string(json, index, charLength), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
                    if(number <= int.MaxValue && number >= int.MinValue)
                    {
                        returnNumber = (int)number;
                    }
                    else
                    {
                        returnNumber = number;
                    }
                }
            }
            index = lastIndex + 1;
            return returnNumber;
        }

        protected static int GetLastIndexOfNumber(char[] json, int index)
        {
            int lastIndex;

            for(lastIndex = index; lastIndex < json.Length; lastIndex++)
            { 
                if("0123456789ABCDEFabcdefxX+-.eE".IndexOf(json[lastIndex]) == -1)
                {
                    break;
                }
            }
            return lastIndex - 1;
        }

        protected static void EatWhitespace(char[] json, ref int index, ref int line)
        {
            for(; index < json.Length; index++)
            {
                if(" \t\n\r\b\f".IndexOf(json[index]) == -1)
                {
                    break;
                }
				else if(json[index] == '\n')
				{
					line++;
				}
            }
        }

        protected static void EatComment(char[] json, ref int index, ref int line)
        {
            while(index < json.Length && json[index] == '/' && json[index + 1] == '*')
            {
                index += 2;
                while(index < json.Length - 1 && !(json[index] == '*' && json[index + 1] == '/'))
                {
					if(json[index] == '\n')
					{
						line++;
					}
                    index++;
                }
                index += 2;
                EatWhitespace(json, ref index, ref line);
            }
        }

        protected static int LookAhead (char[] json, int index, int line)
		{
			int saveIndex = index;
			int saveLine = line;
            return NextToken(json, ref saveIndex, ref saveLine);
        }

        protected static int NextToken(char[] json, ref int index, ref int line)
        {
            EatWhitespace(json, ref index, ref line);
            EatComment(json, ref index, ref line);
            
            if(index >= json.Length)
            {
                return TOKEN_NONE;
            }

            char c = json[index];
            index++;
            switch(c)
            {
            case '{':
                return TOKEN_CURLY_OPEN;
            case '}':
                return TOKEN_CURLY_CLOSE;
            case '[':
                return TOKEN_SQUARED_OPEN;
            case ']':
                return TOKEN_SQUARED_CLOSE;
            case ',':
                return TOKEN_COMMA;
            case '"':
                return TOKEN_STRING;
            case '0':
            case '1':
            case '2':
            case '3':
            case '4':
            case '5':
            case '6':
            case '7':
            case '8':
            case '9':
            case '-':
                return TOKEN_NUMBER;
            case ':':
                return TOKEN_COLON;
            }
            index--;

            int remainingLength = json.Length - index;

            // false
            if(remainingLength >= 5)
            {
                if(json[index] == 'f' &&
                    json[index + 1] == 'a' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 's' &&
                    json[index + 4] == 'e')
                {
                    index += 5;
                    return TOKEN_FALSE;
                }
            }

            // true
            if(remainingLength >= 4)
            {
                if(json[index] == 't' &&
                    json[index + 1] == 'r' &&
                    json[index + 2] == 'u' &&
                    json[index + 3] == 'e')
                {
                    index += 4;
                    return TOKEN_TRUE;
                }
            }

            // null
            if(remainingLength >= 4)
            {
                if(json[index] == 'n' &&
                    json[index + 1] == 'u' &&
                    json[index + 2] == 'l' &&
                    json[index + 3] == 'l')
                {
                    index += 4;
                    return TOKEN_NULL;
                }
            }

            return TOKEN_NONE;
        }

        protected static bool SerializeValue(IJsonSerializerStrategy jsonSerializerStrategy, object value, StringBuilder builder)
        {
            bool success = true;

            if(value is string)
            {
                success = SerializeString((string)value, builder);
            }
            else
            if(value is IDictionary<string, object>)
            {
                IDictionary<string, object> dict = (IDictionary<string, object>)value;
                success = SerializeObject(jsonSerializerStrategy, dict.Keys, dict.Values, builder);
            }
            else
            if(value is IDictionary<string, string>)
            {
                IDictionary<string, string> dict = (IDictionary<string, string>)value;
                success = SerializeObject(jsonSerializerStrategy, dict.Keys, dict.Values, builder);
            }
            else
            if(value is IEnumerable)
            {
                success = SerializeArray(jsonSerializerStrategy, (IEnumerable)value, builder);
            }
            else
            if(IsNumeric(value))
            {
                success = SerializeNumber(value, builder);
            }
            else
            if(value is Boolean)
            {
                builder.Append((bool)value ? "true" : "false");
            }
            else
            if(value == null)
            {
                builder.Append("null");
            }
            else
            {
                object serializedObject;
                success = jsonSerializerStrategy.SerializeNonPrimitiveObject(value, out serializedObject);
                if(success)
                {
                    SerializeValue(jsonSerializerStrategy, serializedObject, builder);
                }
            }

            return success;
        }

        protected static bool SerializeObject(IJsonSerializerStrategy jsonSerializerStrategy, IEnumerable keys, IEnumerable values, StringBuilder builder)
        {
            builder.Append("{");

            IEnumerator ke = keys.GetEnumerator();
            IEnumerator ve = values.GetEnumerator();

            bool first = true;
            while(ke.MoveNext() && ve.MoveNext())
            {
                object key = ke.Current;
                object value = ve.Current;

                if(!first)
                {
                    builder.Append(",");
                }

                if(key is string)
                {
                    SerializeString((string)key, builder);
                }
                else
                if(!SerializeValue(jsonSerializerStrategy, value, builder))
                {
                    return false;
                }

                builder.Append(":");
                if(!SerializeValue(jsonSerializerStrategy, value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append("}");
            return true;
        }

        protected static bool SerializeArray(IJsonSerializerStrategy jsonSerializerStrategy, IEnumerable anArray, StringBuilder builder)
        {
            builder.Append("[");

            bool first = true;
            foreach(object value in anArray)
            {
                if(!first)
                {
                    builder.Append(",");
                }

                if(!SerializeValue(jsonSerializerStrategy, value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append("]");
            return true;
        }

        protected static bool SerializeString(string aString, StringBuilder builder)
        {
            builder.Append("\"");

            char[] charArray = aString.ToCharArray();
            for(int i = 0; i < charArray.Length; i++)
            {
                char c = charArray[i];
                if(c == '"')
                {
                    builder.Append("\\\"");
                }
                else
                if(c == '\\')
                {
                    builder.Append("\\\\");
                }
                else
                if(c == '\b')
                {
                    builder.Append("\\b");
                }
                else
                if(c == '\f')
                {
                    builder.Append("\\f");
                }
                else
                if(c == '\n')
                {
                    builder.Append("\\n");
                }
                else
                if(c == '\r')
                {
                    builder.Append("\\r");
                }
                else
                if(c == '\t')
                {
                    builder.Append("\\t");
                }
                else
                {
                    builder.Append(c);
                }
            }

            builder.Append("\"");
            return true;
        }

        protected static bool SerializeNumber(object number, StringBuilder builder)
        {
            if(number is long)
            {
                builder.Append(((long)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            if(number is ulong)
            {
                builder.Append(((ulong)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            if(number is int)
            {
                builder.Append(((int)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            if(number is uint)
            {
                builder.Append(((uint)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            if(number is decimal)
            {
                builder.Append(((decimal)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            if(number is float)
            {
                builder.Append(((float)number).ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(Convert.ToDouble(number, CultureInfo.InvariantCulture).ToString("r", CultureInfo.InvariantCulture));
            }

            return true;
        }

        /// <summary>
        /// Determines if a given object is numeric in any way
        /// (can be integer, double, null, etc).
        /// </summary>
        protected static bool IsNumeric(object value)
        {
            if(value is sbyte)
            {
                return true;
            }
            if(value is byte)
            {
                return true;
            }
            if(value is short)
            {
                return true;
            }
            if(value is ushort)
            {
                return true;
            }
            if(value is int)
            {
                return true;
            }
            if(value is uint)
            {
                return true;
            }
            if(value is long)
            {
                return true;
            }
            if(value is ulong)
            {
                return true;
            }
            if(value is float)
            {
                return true;
            }
            if(value is double)
            {
                return true;
            }
            if(value is decimal)
            {
                return true;
            }
            return false;
        }

        private static IJsonSerializerStrategy currentJsonSerializerStrategy;

        public static IJsonSerializerStrategy CurrentJsonSerializerStrategy
        {
            get
            {
                // todo: implement locking mechanism.
                return currentJsonSerializerStrategy ?? (currentJsonSerializerStrategy = PocoJsonSerializerStrategy);
            }

            set
            {
                currentJsonSerializerStrategy = value;
            }
        }

        private static PocoJsonSerializerStrategy pocoJsonSerializerStrategy;

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
        public static PocoJsonSerializerStrategy PocoJsonSerializerStrategy
        {
            get
            {
                // todo: implement locking mechanism.
                return pocoJsonSerializerStrategy ?? (pocoJsonSerializerStrategy = new PocoJsonSerializerStrategy());
            }
        }
    }

    #endregion

    #region Simple Json Serializer Strategies

    public interface IJsonSerializerStrategy
    {
        bool SerializeNonPrimitiveObject(object input, out object output);

        object DeserializeObject(object value, Type type);
    }

    public class PocoJsonSerializerStrategy : IJsonSerializerStrategy
    {
        internal CacheResolver CacheResolver;
        private static readonly string[] Iso8601Format = new string[]
                                                             {
                                                                 @"yyyy-MM-dd\THH:mm:ss.FFFFFFF\Z",
                                                                 @"yyyy-MM-dd\THH:mm:ss\Z",
                                                                 @"yyyy-MM-dd\THH:mm:ssK"
                                                             };

        public PocoJsonSerializerStrategy()
        {
            CacheResolver = new CacheResolver(BuildMap);
        }

        protected virtual void BuildMap(Type type, SafeDictionary<string, CacheResolver.MemberMap> memberMaps)
        {
            foreach(PropertyInfo info in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                memberMaps.Add(info.Name, new CacheResolver.MemberMap(info));
            }
            foreach(FieldInfo info in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                memberMaps.Add(info.Name, new CacheResolver.MemberMap(info));
            }
        }

        public virtual bool SerializeNonPrimitiveObject(object input, out object output)
        {
            return TrySerializeKnownTypes(input, out output) || TrySerializeUnknownTypes(input, out output);
        }

        public virtual object DeserializeObject(object value, Type type)
        {
            object obj = null;
            if(value is string)
            {
                string str = value as string;
                if(!string.IsNullOrEmpty(str) && (type == typeof(DateTime) || (ReflectionUtils.IsNullableType(type) && Nullable.GetUnderlyingType(type) == typeof(DateTime))))
                {
                    obj = DateTime.ParseExact(str, Iso8601Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                else
                {
                    obj = str;
                }
            }
            else
            if(value is bool)
            {
                obj = value;
            }
            else
            if(value == null)
            {
                obj = null;
            }
            else
            if((value is long && type == typeof(long)) || (value is double && type == typeof(double)) || (value is int && type == typeof(int)))
            {
                obj = value;
            }
            else
            if((value is double && type != typeof(double)) || (value is long && type != typeof(long)) || (value is int && type != typeof(int)))
            {
                obj = typeof(IConvertible).IsAssignableFrom(type) ? Convert.ChangeType(value, type, CultureInfo.InvariantCulture) : value;
            }
            else
            {
                if(value is IDictionary<string, object>)
                {
                    IDictionary<string, object> jsonObject = (IDictionary<string, object>)value;

                    if(ReflectionUtils.IsTypeDictionary(type))
                    {
                        // if dictionary then
                        Type keyType = type.GetGenericArguments()[0];
                        Type valueType = type.GetGenericArguments()[1];

                        Type genericType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                        IDictionary dict = (IDictionary)CacheResolver.GetNewInstance(genericType);
                        foreach(KeyValuePair<string, object> kvp in jsonObject)
                        {
                            dict.Add(kvp.Key, DeserializeObject(kvp.Value, valueType));
                        }

                        obj = dict;
                    }
                    else
                    {
                        obj = CacheResolver.GetNewInstance(type);
                        SafeDictionary<string, CacheResolver.MemberMap> maps = CacheResolver.LoadMaps(type);

                        if(maps == null)
                        {
                            obj = value;
                        }
                        else
                        {
                            foreach(KeyValuePair<string, CacheResolver.MemberMap> keyValuePair in maps)
                            {
                                CacheResolver.MemberMap v = keyValuePair.Value;
                                if(v.Setter == null)
                                {
                                    continue;
                                }

                                string jsonKey = keyValuePair.Key;
                                if(jsonObject.ContainsKey(jsonKey))
                                {
                                    object jsonValue = DeserializeObject(jsonObject[jsonKey], v.Type);
                                    v.Setter(obj, jsonValue);
                                }
                            }
                        }
                    }
                }
                else
                if(value is IList<object>)
                {
                    IList<object> jsonObject = (IList<object>)value;
                    IList list = null;

                    if(type.IsArray)
                    {
                        list = (IList)Activator.CreateInstance(type, jsonObject.Count);
                        int i = 0;
                        foreach(object o in jsonObject)
                            list[i++] = DeserializeObject(o, type.GetElementType());
                    }
                    else
                    if(ReflectionUtils.IsTypeGenericeCollectionInterface(type) || typeof(IList).IsAssignableFrom(type))
                    {
                        Type innerType = type.GetGenericArguments()[0];
                        Type genericType = typeof(List<>).MakeGenericType(innerType);
                        list = (IList)CacheResolver.GetNewInstance(genericType);
                        foreach(object o in jsonObject)
                            list.Add(DeserializeObject(o, innerType));
                    }

                    obj = list;
                }
            }

            if(obj == null && value != null)
            {
                Logging.Logger.Log(Logging.LogLevel.Warning, "SimpleJson: Failed to properly deserialize value '{0}' of type '{1}'!", value, value.GetType());
            }

            if(ReflectionUtils.IsNullableType(type))
            {
                return ReflectionUtils.ToNullableType(obj, type);
            }

            return obj;
        }

        protected virtual object SerializeEnum(Enum p)
        {
            return Convert.ToDouble(p, CultureInfo.InvariantCulture);
        }

        protected virtual bool TrySerializeKnownTypes(object input, out object output)
        {
            bool returnValue = true;
            if(input is DateTime)
            {
                output = ((DateTime)input).ToUniversalTime().ToString(Iso8601Format[0], CultureInfo.InvariantCulture);
            }
            else
            if(input is Guid)
            {
                output = ((Guid)input).ToString("D");
            }
            else
            if(input is Uri)
            {
                output = input.ToString();
            }
            else
            if(input is Enum)
            {
                output = SerializeEnum((Enum)input);
            }
            else
            {
                returnValue = false;
                output = null;
            }

            return returnValue;
        }

        protected virtual bool TrySerializeUnknownTypes(object input, out object output)
        {
            output = null;

            // todo: implement caching for types
            Type type = input.GetType();

            if(type.FullName == null)
            {
                return false;
            }

            IDictionary<string, object> obj = new JsonObject();

            SafeDictionary<string, CacheResolver.MemberMap> maps = CacheResolver.LoadMaps(type);

            foreach(KeyValuePair<string, CacheResolver.MemberMap> keyValuePair in maps)
            {
                if(keyValuePair.Value.Getter != null)
                {
                    obj.Add(keyValuePair.Key, keyValuePair.Value.Getter(input));
                }
            }

            output = obj;
            return true;
        }
    }
    #endregion

    #region Reflection helpers

    namespace Reflection
    {
        internal class ReflectionUtils
        {
            public static Attribute GetAttribute(MemberInfo info, Type type)
            {
                if(info == null || type == null || !Attribute.IsDefined(info, type))
                {
                    return null;
                }
                return Attribute.GetCustomAttribute(info, type);
            }

            public static Attribute GetAttribute(Type objectType, Type attributeType)
            {

                if(objectType == null || attributeType == null || !Attribute.IsDefined(objectType, attributeType))
                {
                    return null;
                }
                return Attribute.GetCustomAttribute(objectType, attributeType);
            }

            public static bool IsTypeGenericeCollectionInterface(Type type)
            {
                if(!type.IsGenericType)
                {
                    return false;
                }

                Type genericDefinition = type.GetGenericTypeDefinition();

                return (genericDefinition == typeof(IList<>) || genericDefinition == typeof(ICollection<>) || genericDefinition == typeof(IEnumerable<>));
            }

            public static bool IsTypeDictionary(Type type)
            {
                if(typeof(IDictionary).IsAssignableFrom(type))
                {
                    return true;
                }

                if(!type.IsGenericType)
                {
                    return false;
                }
                Type genericDefinition = type.GetGenericTypeDefinition();
                return genericDefinition == typeof(IDictionary<,>);
            }

            public static bool IsNullableType(Type type)
            {
                return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
            }

            public static object ToNullableType(object obj, Type nullableType)
            {
                return obj == null ? null : Convert.ChangeType(obj, Nullable.GetUnderlyingType(nullableType), CultureInfo.InvariantCulture);
            }
        }

        public delegate object GetHandler(object source);

        public delegate void SetHandler(object source,object value);

        public delegate void MemberMapLoader(Type type,SafeDictionary<string, CacheResolver.MemberMap> memberMaps);

        public class CacheResolver
        {
            private readonly MemberMapLoader _memberMapLoader;
            private readonly SafeDictionary<Type, SafeDictionary<string, MemberMap>> _memberMapsCache = new SafeDictionary<Type, SafeDictionary<string, MemberMap>>();

            delegate object CtorDelegate();

            readonly static SafeDictionary<Type, CtorDelegate> ConstructorCache = new SafeDictionary<Type, CtorDelegate>();

            public CacheResolver(MemberMapLoader memberMapLoader)
            {
                _memberMapLoader = memberMapLoader;
            }

            [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")]
            public static object GetNewInstance(Type type)
            {
                CtorDelegate c;
                if(ConstructorCache.TryGetValue(type, out c))
                {
                    return c();
                }

                ConstructorInfo constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

                c = delegate
                {
                    return constructorInfo.Invoke(null);
                };
                ConstructorCache.Add(type, c);
                return c();
            }

            public SafeDictionary<string, MemberMap> LoadMaps(Type type)
            {
                if(type == null || type == typeof(object))
                {
                    return null;
                }
                SafeDictionary<string, MemberMap> maps;
                if(_memberMapsCache.TryGetValue(type, out maps))
                {
                    return maps;
                }
                maps = new SafeDictionary<string, MemberMap>();
                _memberMapLoader(type, maps);
                _memberMapsCache.Add(type, maps);
                return maps;
            }

            static GetHandler CreateGetHandler(FieldInfo fieldInfo)
            {
                return delegate(object instance)
                {
                    return fieldInfo.GetValue(instance);
                };
            }

            static SetHandler CreateSetHandler(FieldInfo fieldInfo)
            {
                if(fieldInfo.IsInitOnly || fieldInfo.IsLiteral)
                {
                    return null;
                }
                return delegate(object instance, object value)
                {
                    fieldInfo.SetValue(instance, value);
                };
            }

            static GetHandler CreateGetHandler(PropertyInfo propertyInfo)
            {
                MethodInfo getMethodInfo = propertyInfo.GetGetMethod(true);
                if(getMethodInfo == null)
                {
                    return null;
                }

                return delegate(object instance)
                {
                    return getMethodInfo.Invoke(instance, Type.EmptyTypes);
                };
            }

            static SetHandler CreateSetHandler(PropertyInfo propertyInfo)
            {
                MethodInfo setMethodInfo = propertyInfo.GetSetMethod(true);
                if(setMethodInfo == null)
                {
                    return null;
                }
                return delegate(object instance, object value)
                {
                    setMethodInfo.Invoke(instance, new[] { value });
                };
            }

            public sealed class MemberMap
            {
                public readonly MemberInfo MemberInfo;
                public readonly Type Type;
                public readonly GetHandler Getter;
                public readonly SetHandler Setter;

                public MemberMap(PropertyInfo propertyInfo)
                {
                    MemberInfo = propertyInfo;
                    Type = propertyInfo.PropertyType;
                    Getter = CreateGetHandler(propertyInfo);
                    Setter = CreateSetHandler(propertyInfo);
                }

                public MemberMap(FieldInfo fieldInfo)
                {
                    MemberInfo = fieldInfo;
                    Type = fieldInfo.FieldType;
                    Getter = CreateGetHandler(fieldInfo);
                    Setter = CreateSetHandler(fieldInfo);
                }
            }
        }

        public class SafeDictionary<TKey, TValue>
        {
            private readonly object _padlock = new object();
            private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

            public bool TryGetValue(TKey key, out TValue value)
            {
                return _dictionary.TryGetValue(key, out value);
            }

            public TValue this[TKey key]
            {
                get { return _dictionary[key]; }
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).GetEnumerator();
            }

            public void Add(TKey key, TValue value)
            {
                lock(_padlock)
                {
                    if(_dictionary.ContainsKey(key) == false)
                    {
                        _dictionary.Add(key, value);
                    }
                }
            }
        }
    }

    #endregion
}
