//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities
{
    /// <summary>
    /// Wrapper class for fluent conditional actions.
    /// </summary>
    public class IfWrapper<T>
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="value">The object instance being wrapped.</param>
        /// <param name="condition">The condition which will be used by
        ///   <see cref="Then"/> and <see cref="Else"/>.</param>
        public IfWrapper(T value, bool condition)
        {
            this.value = value;
            this.condition = condition;
        }

        /// <summary>
        /// Executes the specified action on the wrapped object if the condition is true.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The current <see cref="IfWrapper{T}"/> instance.</returns>
        public IfWrapper<T> Then(Action<T> action)
        {
            if(condition)
            {
                action(value);
            }
            return this;
        }

        /// <summary>
        /// Executes the specified action on the wrapped object if the condition is false.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>The wrapped object.</returns>
        public T Else(Action<T> action)
        {
            if(!condition)
            {
                action(value);
            }
            return value;
        }

        /// <summary>
        /// Ends the conditional block and returns the wrapped object.
        /// </summary>
        /// <returns>The wrapped object.</returns>
        public T EndIf()
        {
            return value;
        }

        private readonly T value;
        private readonly bool condition;
    }
}
