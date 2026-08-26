using System;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点输出变量描述，供变量选择器和绑定校验使用。
    /// </summary>
    public sealed class NodeOutputDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public FlowDataType DataType { get; set; }

        /// <summary>
        /// 获取或设置字符串协议值对应的具体枚举类型；为空表示普通数据类型。
        /// 变量绑定只有在目标设置声明同一枚举类型时才兼容。
        /// </summary>
        public Type EnumType { get; set; }

        /// <summary>
        /// 获取或设置 Object 输出承载的 CLR 契约类型；为空表示类型未知。
        /// 设计器据此生成最多一层公开成员候选，该元数据不写入流程文件。
        /// </summary>
        public Type ObjectType { get; set; }

        public string Description { get; set; }
    }
}
