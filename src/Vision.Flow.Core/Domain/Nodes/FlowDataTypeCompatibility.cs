using System;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 数据类型赋值兼容性；Warning 为兼容保留值，严格配置绑定不再返回该状态。
    /// </summary>
    public enum FlowDataTypeCompatibilityResult
    {
        Incompatible = 0,
        Compatible = 1,
        Warning = 2
    }

    /// <summary>
    /// 为校验器和设计器提供统一的严格配置类型规则。
    /// </summary>
    public static class FlowDataTypeCompatibility
    {
        public static FlowDataTypeCompatibilityResult GetCompatibility(FlowDataType source, FlowDataType target)
        {
            if (source == FlowDataType.Control || target == FlowDataType.Control)
            {
                return FlowDataTypeCompatibilityResult.Incompatible;
            }

            if (source == target)
            {
                return FlowDataTypeCompatibilityResult.Compatible;
            }

            return FlowDataTypeCompatibilityResult.Incompatible;
        }

        public static bool IsCompatible(FlowDataType source, FlowDataType target)
        {
            return GetCompatibility(source, target) != FlowDataTypeCompatibilityResult.Incompatible;
        }

        /// <summary>
        /// 判断变量来源与目标设置的基础类型及具体枚举类型是否兼容。
        /// 只要任一侧声明枚举元数据，两侧就必须声明同一个有效枚举类型。
        /// </summary>
        public static bool IsCompatible(
            FlowDataType source,
            Type sourceEnumType,
            FlowDataType target,
            Type targetEnumType)
        {
            return IsCompatible(
                source,
                sourceEnumType,
                null,
                target,
                targetEnumType,
                null);
        }

        /// <summary>
        /// 判断变量来源与目标设置的基础类型、枚举类型及 Object CLR 契约是否兼容。
        /// 未声明目标 ObjectType 的通用 Object 可接收任意 Object；反向绑定不成立。
        /// </summary>
        public static bool IsCompatible(
            FlowDataType source,
            Type sourceEnumType,
            Type sourceObjectType,
            FlowDataType target,
            Type targetEnumType,
            Type targetObjectType)
        {
            if (!IsCompatible(source, target))
            {
                return false;
            }

            if (sourceEnumType != null || targetEnumType != null)
            {
                return sourceEnumType != null &&
                    targetEnumType != null &&
                    sourceEnumType.IsEnum &&
                    targetEnumType.IsEnum &&
                    sourceEnumType == targetEnumType;
            }

            if (source != FlowDataType.Object)
            {
                return sourceObjectType == null && targetObjectType == null;
            }

            if (targetObjectType == null)
            {
                return true;
            }

            return sourceObjectType != null &&
                targetObjectType.IsAssignableFrom(sourceObjectType);
        }
    }

    /// <summary>
    /// 描述 CLR 类型在节点 Descriptor 中对应的基础类型和附加类型元数据。
    /// </summary>
    public sealed class FlowTypeMetadata
    {
        private FlowTypeMetadata(
            FlowDataType dataType,
            Type enumType,
            Type objectType)
        {
            DataType = dataType;
            EnumType = enumType;
            ObjectType = objectType;
        }

        /// <summary>获取流程基础数据类型。</summary>
        public FlowDataType DataType { get; private set; }

        /// <summary>获取字符串协议承载的枚举类型。</summary>
        public Type EnumType { get; private set; }

        /// <summary>获取 Object 值的 CLR 契约类型。</summary>
        public Type ObjectType { get; private set; }

        /// <summary>
        /// 将公开 CLR 类型映射为 Designer 和 Validator 使用的 Descriptor 类型元数据。
        /// 未知引用类型保持为带明确 ObjectType 的 Object。
        /// </summary>
        public static FlowTypeMetadata FromClrType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            Type effective = Nullable.GetUnderlyingType(type) ?? type;
            if (effective.IsEnum)
                return new FlowTypeMetadata(FlowDataType.String, effective, null);
            if (effective == typeof(string) || effective == typeof(char))
                return new FlowTypeMetadata(FlowDataType.String, null, null);
            if (effective == typeof(int) || effective == typeof(short) ||
                effective == typeof(ushort) || effective == typeof(byte) ||
                effective == typeof(sbyte))
            {
                return new FlowTypeMetadata(FlowDataType.Int32, null, null);
            }
            if (effective == typeof(long) || effective == typeof(uint) ||
                effective == typeof(ulong))
            {
                return new FlowTypeMetadata(FlowDataType.Int64, null, null);
            }
            if (effective == typeof(bool))
                return new FlowTypeMetadata(FlowDataType.Boolean, null, null);
            if (effective == typeof(double) || effective == typeof(float) ||
                effective == typeof(decimal))
            {
                return new FlowTypeMetadata(FlowDataType.Double, null, null);
            }
            if (effective == typeof(DateTime))
                return new FlowTypeMetadata(FlowDataType.DateTime, null, null);
            if (typeof(IVisionImage).IsAssignableFrom(effective))
                return new FlowTypeMetadata(FlowDataType.IVisionImage, null, null);
            if (typeof(CameraFrameData).IsAssignableFrom(effective))
                return new FlowTypeMetadata(FlowDataType.CameraFrameData, null, null);

            return new FlowTypeMetadata(FlowDataType.Object, null, effective);
        }
    }
}
