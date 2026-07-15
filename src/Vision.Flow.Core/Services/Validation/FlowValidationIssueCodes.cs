namespace Vision.Flow.Core.Services.Validation
{
    /// <summary>
    /// 流程校验错误码常量。外部工具和测试可能依赖这些错误码做自动判断。
    /// </summary>
    public static class FlowValidationIssueCodes
    {
        public const string FlowDesignMissing = "FlowDesignMissing";
        public const string RuntimeMissing = "RuntimeMissing";
        public const string SchemaVersionUnsupported = "SchemaVersionUnsupported";
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
        public const string NodeExecutionPolicyInvalid = "NodeExecutionPolicyInvalid";
        public const string NodeErrorPortMissing = "NodeErrorPortMissing";
        public const string NodeDefaultOutputInvalid = "NodeDefaultOutputInvalid";
        public const string EdgeMissing = "EdgeMissing";
        public const string EdgeSourceMissing = "EdgeSourceMissing";
        public const string EdgeTargetMissing = "EdgeTargetMissing";
        public const string EdgeFromPortMissing = "EdgeFromPortMissing";
        public const string EdgeToPortMissing = "EdgeToPortMissing";
        public const string EdgeSourcePortUnknown = "EdgeSourcePortUnknown";
        public const string EdgeToPortUnknown = "EdgeToPortUnknown";
        public const string EdgeTargetPortUnknown = "EdgeTargetPortUnknown";
        public const string FlowCycleDetected = "FlowCycleDetected";
        public const string EntriesMissing = "EntriesMissing";
        public const string EntryMissing = "EntryMissing";
        public const string EntryNameMissing = "EntryNameMissing";
        public const string EntryNameDuplicate = "EntryNameDuplicate";
        public const string EntryTargetMissing = "EntryTargetMissing";
        public const string EntryTargetNotFound = "EntryTargetNotFound";
        public const string EntryTriggerKindInvalid = "EntryTriggerKindInvalid";
        public const string EntrySourceMissing = "EntrySourceMissing";
        public const string EntrySourceNotFound = "EntrySourceNotFound";
        public const string EntrySourceNotListener = "EntrySourceNotListener";
        public const string EntrySourceDuplicate = "EntrySourceDuplicate";
        public const string EntryInputInvalid = "EntryInputInvalid";
        public const string EntryInputNameDuplicate = "EntryInputNameDuplicate";
        public const string EntryInputDefaultInvalid = "EntryInputDefaultInvalid";
        public const string EntryExecutionPolicyInvalid = "EntryExecutionPolicyInvalid";
        public const string RequiredSettingMissing = "RequiredSettingMissing";
        public const string RuntimeContainsViewState = "RuntimeContainsViewState";
        public const string DuplicatePolicyInvalid = "DuplicatePolicyInvalid";
        public const string SettingValueInvalid = "SettingValueInvalid";
        public const string VariableSelectorInvalid = "VariableSelectorInvalid";
        public const string VariableSelectorNotAllowed = "VariableSelectorNotAllowed";
        public const string VariableSourceNodeMissing = "VariableSourceNodeMissing";
        public const string VariableSourceNotUpstream = "VariableSourceNotUpstream";
        public const string VariableSourceNotGuaranteed = "VariableSourceNotGuaranteed";
        public const string VariableOutputMissing = "VariableOutputMissing";
        public const string VariableTypeIncompatible = "VariableTypeIncompatible";
        public const string VariableTypeWarning = "VariableTypeWarning";
        public const string TriggerInputUnavailable = "TriggerInputUnavailable";
        public const string TriggerInputNotGuaranteed = "TriggerInputNotGuaranteed";
        public const string TriggerInputTypeConflict = "TriggerInputTypeConflict";
    }
}
