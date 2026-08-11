using System;
using System.Collections.Generic;
using System.Linq;

namespace FakeCreatorCore
{
    public static class Helpers
    {
        private static readonly HashSet<string> BaseTypes = new HashSet<string>
        {
            "Boolean", "Byte", "Char", "DateTime", "DateTimeOffset", "Decimal",
            "Double", "Int16", "Int32", "Int64", "SByte", "Single", "String",
            "UInt16", "UInt32", "UInt64"
        };

        public static bool IsTypeAGenericSimpleType(this Type typeToCheck)
        {
            return typeToCheck.IsGenericType && typeToCheck.GetGenericArguments().All(r=>IsTypeASimpleType(r));
        }

        public static bool IsASimpleType(this string value)
        {
            return value != null && BaseTypes.Contains(value);
        }


        public static bool IsNullableEnum(this Type typeToCheck)
        {
            return typeToCheck.IsGenericType && typeToCheck.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsTypeASimpleType(this Type typeToCheck)
        {
            var typeCode = Type.GetTypeCode(GetUnderlyingType(typeToCheck));

            switch (typeCode)
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        public static Type GetUnderlyingType(Type typeToCheck)
        {
            if (typeToCheck.IsGenericType &&
                typeToCheck.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Nullable.GetUnderlyingType(typeToCheck);
            }
            else
            {
                return typeToCheck;
            }
        }
    }
}