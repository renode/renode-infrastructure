//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public interface IIdentifiable
    {
        int UniqueObjectId { get; }
    }

    public static class IdentifiableExtensions
    {
        public static string GetDescription(this IIdentifiable @this)
        {
            return $"{@this.GetType().Name}#{@this.UniqueObjectId}";
        }
    }
}