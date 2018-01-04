//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Mono.Cecil;

namespace Antmicro.Renode.Utilities
{
    public class TypeDescriptor
    {
        public TypeDescriptor(Type t)
        {
            underlyingType = t;
        }

        public TypeDescriptor(TypeDefinition t)
        {
            underlyingType = t;
        }

        public string Name
        {
            get
            {
                var type = underlyingType as Type;
                if (type != null)
                {
                    return type.Name;
                }

                var typeDefinition = underlyingType as TypeDefinition;
                if (typeDefinition != null)
                {
                    return typeDefinition.Name;
                }

                throw new ArgumentException("Unsupported underlying type: " + underlyingType.GetType().FullName);
            }
        }

        public string Namespace
        {
            get
            {
                var type = underlyingType as Type;
                if (type != null)
                {
                    return type.Namespace;
                }

                var typeDefinition = underlyingType as TypeDefinition;
                if (typeDefinition != null)
                {
                    return typeDefinition.Namespace;
                }

                throw new ArgumentException("Unsupported underlying type: " + underlyingType.GetType().FullName);
            }

        }

        public Type ResolveType()
        {
            var type = underlyingType as Type;
            if (type != null)
            {
                return type;
            }

            var typeDefinition = underlyingType as TypeDefinition;
            if (typeDefinition != null)
            {
                return TypeResolver.ResolveType(typeDefinition.GetFullNameOfMember()) ??
                    TypeManager.Instance.GetTypeByName(typeDefinition.GetFullNameOfMember());
            }

            throw new ArgumentException("Unsupported underlying type: " + underlyingType.GetType().FullName);
        }

        private readonly object underlyingType;
    }
}

