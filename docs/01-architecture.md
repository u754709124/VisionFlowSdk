# 01 - Architecture

## 解决方案结构

```text
VisionFlowSdk
│
├─ src
│  ├─ Vision.Flow.Core
│  ├─ Vision.Flow.Nodes
│  ├─ Vision.DeviceAdapters
│  └─ Vision.Flow.Designer.Wpf
│
├─ tests
│  └─ Vision.Flow.Tests
│
└─ demos
   ├─ Vision.Flow.Demo.WinForms
   └─ Vision.Flow.Demo.DesignerWpf
```

## 项目职责

### Vision.Flow.Core

负责：

- 流程定义
- 运行态定义
- 节点接口
- 执行引擎
- Token
- 变量池
- 运行事件
- 流程校验
- 流程序列化
- Adapter 接口

不允许引用：

- WPF
- WinForms
- 具体硬件 SDK

### Vision.Flow.Nodes

负责公共节点：

- Camera Nodes
- Light Nodes
- Motion Nodes
- Recipe Nodes
- Save Nodes
- Join Nodes
- Group Nodes
- Scan Fusion Nodes

节点只调用 Adapter 接口。

### Vision.DeviceAdapters

负责：

- 默认设备注册表
- Fake Adapter
- Adapter 基类
- Demo Adapter

生产真实 Adapter 由上位机应用实现。

### Vision.Flow.Designer.Wpf

负责：

- WPF 设计器控件
- 流程画布
- 节点卡片
- 连线
- 属性面板
- 变量选择器
- 调试面板

不负责生产执行逻辑。

## 依赖方向

允许：

```text
Designer.Wpf -> Core
Designer.Wpf -> Nodes

Nodes -> Core
DeviceAdapters -> Core

Demo.WinForms -> Core
Demo.WinForms -> Nodes
Demo.WinForms -> DeviceAdapters

Demo.DesignerWpf -> Core
Demo.DesignerWpf -> Nodes
Demo.DesignerWpf -> DeviceAdapters
Demo.DesignerWpf -> Designer.Wpf
```

禁止：

```text
Core -> WPF
Core -> WinForms
Core -> specific SDK

Nodes -> WPF
Nodes -> WinForms
Nodes -> specific SDK

Runtime -> Designer UI
```

## 生产运行路径

```text
WinForms App
    -> 注册真实 Adapter
    -> 注册公共节点
    -> 加载 .flowruntime
    -> 创建 FlowRunner
    -> 外部事件 Trigger
    -> RuntimeEvent 输出日志/状态
```

## WPF 调试路径

```text
WPF Designer
    -> 编译当前 .flowdesign
    -> 创建同一个 FlowRunner
    -> 调试运行
    -> 监听 RuntimeEvent
    -> 高亮节点、显示图像、显示耗时
```
