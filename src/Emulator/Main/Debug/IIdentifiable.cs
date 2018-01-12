//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Debugging
{
    public interface IIdentifiable
    {
#if DEBUG
        int UniqueObjectId { get; }
#endif
    }

    public static class IdentifiableExtensions
    {
#if DEBUG
        public static string GetDescription(this IIdentifiable @this)
        {
            return $"{@this.GetType().Name}#{@this.UniqueObjectId}";
        }
#endif
    }
}
