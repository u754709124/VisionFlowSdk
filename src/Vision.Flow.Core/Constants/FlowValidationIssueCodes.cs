using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Constants
{
    /// <summary>
    /// 流程校验错误码常量。外部工具和测试可能依赖这些错误码做自动判断。
    /// </summary>
    public static class FlowValidationIssueCodes
    {
        public const string FlowDesignMissing = "FlowDesignMissing";
        public const string RuntimeMissing = "RuntimeMissing";
        public const string FlowIdMissing = "FlowIdMissing";
        public const string NodesMissing = "NodesMissing";
        public const string NodeMissing = "NodeMissing";
        public const string NodeIdMissing = "NodeIdMissing";
        public const string NodeIdDuplicate = "NodeIdDuplicate";
        public const string NodeTypeMissing = "NodeTypeMissing";
        public const string NodeTypeNotRegistered = "NodeTypeNotRegistered";
        public const string NodeDescriptorMissing = "NodeDescriptorMissing";
        public const string NodeDescriptorTypeMissing = "NodeDescriptorTypeMissing";
        public const string NodeDescriptorTypeMismatch = "NodeDescriptorTypeMismatch";
        public const string EdgeMissing = "EdgeMissing";
        public const string EdgeSourceMissing = "EdgeSourceMissing";
        public const string EdgeTargetMissing = "EdgeTargetMissing";
        public const string EdgeFromPortMissing = "EdgeFromPortMissing";
        public const string EdgeToPortMissing = "EdgeToPortMissing";
        public const string EdgeSourcePortUnknown = "EdgeSourcePortUnknown";
        public const string EdgeToPortUnknown = "EdgeToPortUnknown";
        public const string EdgeTargetPortUnknown = "EdgeTargetPortUnknown";
        public const string EntriesMissing = "EntriesMissing";
        public const string EntryMissing = "EntryMissing";
        public const string EntryNameMissing = "EntryNameMissing";
        public const string EntryNameDuplicate = "EntryNameDuplicate";
        public const string EntryTargetMissing = "EntryTargetMissing";
        public const string EntryTargetNotFound = "EntryTargetNotFound";
        public const string RequiredSettingMissing = "RequiredSettingMissing";
        public const string BindingInvalid = "BindingInvalid";
        public const string BindingSourceNodeMissing = "BindingSourceNodeMissing";
        public const string BindingSourceMissing = "BindingSourceMissing";
        public const string BindingSourceNotFound = "BindingSourceNotFound";
        public const string BindingOutputMissing = "BindingOutputMissing";
        public const string RuntimeContainsViewState = "RuntimeContainsViewState";
        public const string CameraCallbackModeInvalid = "CameraCallbackModeInvalid";
        public const string CameraMatchModeInvalid = "CameraMatchModeInvalid";
        public const string CameraStreamOutputModeInvalid = "CameraStreamOutputModeInvalid";
        public const string QueueNameMissing = "QueueNameMissing";
        public const string QueueFullModeInvalid = "QueueFullModeInvalid";
        public const string DuplicatePolicyInvalid = "DuplicatePolicyInvalid";
        public const string SettingValueInvalid = "SettingValueInvalid";
    }
}
