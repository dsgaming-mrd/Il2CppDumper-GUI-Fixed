using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Il2CppDumper
{
    public abstract class Il2Cpp : BinaryStream
    {
        public Metadata Metadata;
        private Il2CppMetadataRegistration pMetadataRegistration;
        private Il2CppCodeRegistration pCodeRegistration;
        private Metadata metadata;
        public ulong[] methodPointers;
        public ulong[] genericMethodPointers;
        public ulong[] invokerPointers;
        public ulong[] customAttributeGenerators;
        public ulong[] reversePInvokeWrappers;
        public ulong[] unresolvedVirtualCallPointers;
        private ulong[] fieldOffsets;
        public Il2CppType[] types;
        private readonly Dictionary<ulong, Il2CppType> typeDic = new();
        public ulong[] metadataUsages;
        private Il2CppGenericMethodFunctionsDefinitions[] genericMethodTable;
        public ulong[] genericInstPointers;
        public Il2CppGenericInst[] genericInsts;
        public Il2CppMethodSpec[] methodSpecs;
        public Dictionary<int, List<Il2CppMethodSpec>> methodDefinitionMethodSpecs = new();
        public Dictionary<Il2CppMethodSpec, ulong> methodSpecGenericMethodPointers = new();
        private bool fieldOffsetsArePointers;
        protected long metadataUsagesCount;
        public Dictionary<string, Il2CppCodeGenModule> codeGenModules;
        public Dictionary<string, ulong[]> codeGenModuleMethodPointers;
        private Dictionary<string, Il2CppCodeGenModule> normalizedCodeGenModules;
        private Dictionary<string, ulong[]> normalizedCodeGenModuleMethodPointers;
        private Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>> normalizedRGCTXDataDictionary;
        public Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>> rgctxsDictionary;
        public bool IsDumped;

        public abstract ulong MapVATR(ulong addr);
        public abstract ulong MapRTVA(ulong addr);
        public abstract bool Search();
        public abstract bool PlusSearch(int methodCount, int typeDefinitionsCount, int imageCount);
        public abstract bool SymbolSearch();
        public abstract SectionHelper GetSectionHelper(int methodCount, int typeDefinitionsCount, int imageCount);
        public abstract bool CheckDump();

        protected Il2Cpp(Stream stream) : base(stream) { }

        public void SetProperties(double version, long metadataUsagesCount, Metadata metadata = null)
        {
            Version = version;
            this.metadataUsagesCount = metadataUsagesCount;
            this.metadata = metadata;
        }

        protected bool AutoPlusInit(ulong codeRegistration, ulong metadataRegistration)
        {
            if (codeRegistration != 0)
            {
                var limit = this is WebAssemblyMemory ? 0x35000u : 0x50000u; //TODO
                if (Version >= 24.2)
                {
                    pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
                    if (Version == 31)
                    {
                        if (metadata != null && metadata.header.methodsSize % 40 == 0 && metadata.header.methodsSize % 36 != 0)
                        {
                            Version = 31.1;
                            metadata.Version = 31.1;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                    if ((Version >= 31 && Version < 32) || Version >= 38)
                    {
                        if (pCodeRegistration.genericMethodPointersCount > limit)
                        {
                            codeRegistration -= PointerSize * 2;
                        }
                        else if (Version == 31)
                        {
                            Version = 29;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 29)
                    {
                        if (pCodeRegistration.genericMethodPointersCount > limit)
                        {
                            Version = 29.1;
                            codeRegistration -= PointerSize * 2;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 27)
                    {
                        if (pCodeRegistration.reversePInvokeWrapperCount > limit)
                        {
                            Version = 27.1;
                            codeRegistration -= PointerSize;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 24.4)
                    {
                        codeRegistration -= PointerSize * 2;
                        if (pCodeRegistration.reversePInvokeWrapperCount > limit)
                        {
                            Version = 24.5;
                            codeRegistration -= PointerSize;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                    if (Version == 24.2)
                    {
                        if (pCodeRegistration.interopDataCount == 0) //TODO
                        {
                            Version = 24.3;
                            codeRegistration -= PointerSize * 2;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                    }
                }
            }
            MainForm.Log("CodeRegistration : {0:x}", codeRegistration);
            MainForm.Log("MetadataRegistration : {0:x}", metadataRegistration);
            if (codeRegistration != 0 && metadataRegistration != 0)
            {
                Init(codeRegistration, metadataRegistration);
                return true;
            }
            return false;
        }

        public virtual void Init(ulong codeRegistration, ulong metadataRegistration)
        {
            pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            var limit = this is WebAssemblyMemory ? 0x35000u : 0x50000u; //TODO
            if (Version == 27 && pCodeRegistration.invokerPointersCount > limit)
            {
                Version = 27.1;
                MainForm.Log($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }
            if (Version == 27.1)
            {
                var pCodeGenModules = MapVATR<ulong>(pCodeRegistration.codeGenModules, pCodeRegistration.codeGenModulesCount);
                foreach (var pCodeGenModule in pCodeGenModules)
                {
                    var codeGenModule = MapVATR<Il2CppCodeGenModule>(pCodeGenModule);
                    if (Version < 108 && codeGenModule.rgctxsCount > 0)
                    {
                        var rgctxs = MapVATR<Il2CppRGCTXDefinition>(codeGenModule.rgctxs, codeGenModule.rgctxsCount);
                        if (rgctxs.All(x => x.data.rgctxDataDummy > limit))
                        {
                            Version = 27.2;
                            MainForm.Log($"Change il2cpp version to: {Version}");
                        }
                        break;
                    }
                }
            }
            if (Version == 24.4 && pCodeRegistration.invokerPointersCount > limit)
            {
                Version = 24.5;
                MainForm.Log($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }
            if (Version == 24.2 && pCodeRegistration.codeGenModules == 0) //TODO
            {
                Version = 24.3;
                MainForm.Log($"Change il2cpp version to: {Version}");
                pCodeRegistration = MapVATR<Il2CppCodeRegistration>(codeRegistration);
            }
            pMetadataRegistration = MapVATR<Il2CppMetadataRegistration>(metadataRegistration);
            if (Version == 106)
            {
                var oldVersion = Version;
                try
                {
                    Version = 106.1;
                    var v1061MetadataRegistration = MapVATR<Il2CppMetadataRegistration>(metadataRegistration);
                    if (v1061MetadataRegistration.alwaysInitMetadataUsagesCount > 0 &&
                        v1061MetadataRegistration.alwaysInitMetadataUsagesCount < 0x1000 &&
                        TryMapVATR(v1061MetadataRegistration.alwaysInitMetadataUsages, out _))
                    {
                        pMetadataRegistration = v1061MetadataRegistration;
                        if (metadata != null)
                        {
                            metadata.Version = Version;
                        }
                        MainForm.Log($"Change il2cpp version to: {Version}");
                    }
                    else
                    {
                        Version = oldVersion;
                    }
                }
                catch
                {
                    Version = oldVersion;
                    pMetadataRegistration = MapVATR<Il2CppMetadataRegistration>(metadataRegistration);
                }
            }
            genericMethodPointers = MapVATR<ulong>(pCodeRegistration.genericMethodPointers, pCodeRegistration.genericMethodPointersCount);
            invokerPointers = MapVATR<ulong>(pCodeRegistration.invokerPointers, pCodeRegistration.invokerPointersCount);
            if (Version < 27)
            {
                customAttributeGenerators = MapVATR<ulong>(pCodeRegistration.customAttributeGenerators, pCodeRegistration.customAttributeCount);
            }
            if (Version > 16 && Version < 27)
            {
                metadataUsages = MapVATR<ulong>(pMetadataRegistration.metadataUsages, metadataUsagesCount);
            }
            if (Version >= 22)
            {
                if (pCodeRegistration.reversePInvokeWrapperCount != 0)
                    reversePInvokeWrappers = MapVATR<ulong>(pCodeRegistration.reversePInvokeWrappers, pCodeRegistration.reversePInvokeWrapperCount);
                var unresolvedCallCount = Version >= 35
                    ? pCodeRegistration.unresolvedIndirectCallCount
                    : pCodeRegistration.unresolvedVirtualCallCount;
                if (unresolvedCallCount != 0)
                    unresolvedVirtualCallPointers = MapVATR<ulong>(pCodeRegistration.unresolvedVirtualCallPointers, unresolvedCallCount);
            }
            genericInstPointers = MapVATR<ulong>(pMetadataRegistration.genericInsts, pMetadataRegistration.genericInstsCount);
            genericInsts = Array.ConvertAll(genericInstPointers, MapVATR<Il2CppGenericInst>);
            fieldOffsetsArePointers = Version > 21;
            if (Version == 21)
            {
                var fieldTest = MapVATR<uint>(pMetadataRegistration.fieldOffsets, 6);
                fieldOffsetsArePointers = fieldTest[0] == 0 && fieldTest[1] == 0 && fieldTest[2] == 0 && fieldTest[3] == 0 && fieldTest[4] == 0 && fieldTest[5] > 0;
            }
            if (fieldOffsetsArePointers)
            {
                fieldOffsets = MapVATR<ulong>(pMetadataRegistration.fieldOffsets, pMetadataRegistration.fieldOffsetsCount);
            }
            else
            {
                fieldOffsets = Array.ConvertAll(MapVATR<uint>(pMetadataRegistration.fieldOffsets, pMetadataRegistration.fieldOffsetsCount), x => (ulong)x);
            }
            var pTypes = MapVATR<ulong>(pMetadataRegistration.types, pMetadataRegistration.typesCount);
            types = new Il2CppType[pMetadataRegistration.typesCount];
            for (var i = 0; i < pMetadataRegistration.typesCount; ++i)
            {
                types[i] = MapVATR<Il2CppType>(pTypes[i]);
                types[i].Init(Version);
                typeDic.Add(pTypes[i], types[i]);
            }
            if (Version >= 24.2)
            {
                var pCodeGenModules = MapVATR<ulong>(pCodeRegistration.codeGenModules, pCodeRegistration.codeGenModulesCount);
                codeGenModules = new Dictionary<string, Il2CppCodeGenModule>(pCodeGenModules.Length, StringComparer.OrdinalIgnoreCase);
                normalizedCodeGenModules = new Dictionary<string, Il2CppCodeGenModule>(pCodeGenModules.Length, StringComparer.OrdinalIgnoreCase);
                codeGenModuleMethodPointers = new Dictionary<string, ulong[]>(pCodeGenModules.Length, StringComparer.OrdinalIgnoreCase);
                normalizedCodeGenModuleMethodPointers = new Dictionary<string, ulong[]>(pCodeGenModules.Length, StringComparer.OrdinalIgnoreCase);
                normalizedRGCTXDataDictionary = new Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>>(pCodeGenModules.Length, StringComparer.OrdinalIgnoreCase);
                rgctxsDictionary = new Dictionary<string, Dictionary<uint, Il2CppRGCTXDefinition[]>>(pCodeGenModules.Length, StringComparer.Ordinal);
                foreach (var pCodeGenModule in pCodeGenModules)
                {
                    var codeGenModule = MapVATR<Il2CppCodeGenModule>(pCodeGenModule);
                    var moduleName = ReadStringToNull(MapVATR(codeGenModule.moduleName));
                    codeGenModules[moduleName] = codeGenModule;
                    AddNormalizedModule(normalizedCodeGenModules, moduleName, codeGenModule);
                    ulong[] methodPointers;
                    if (codeGenModule.methodPointers != 0 && TryMapVATR(codeGenModule.methodPointers, out var methodPointersOffset) && methodPointersOffset < Length)
                    {
                        try
                        {
                            methodPointers = ReadClassArray<ulong>(methodPointersOffset, codeGenModule.methodPointerCount);
                        }
                        catch
                        {
                            methodPointers = new ulong[codeGenModule.methodPointerCount];
                        }
                    }
                    else
                    {
                        methodPointers = new ulong[codeGenModule.methodPointerCount];
                    }
                    codeGenModuleMethodPointers[moduleName] = methodPointers;
                    AddNormalizedModule(normalizedCodeGenModuleMethodPointers, moduleName, methodPointers);

                    var rgctxsDefDictionary = new Dictionary<uint, Il2CppRGCTXDefinition[]>();
                    rgctxsDictionary[moduleName] = rgctxsDefDictionary;
                    AddNormalizedModule(normalizedRGCTXDataDictionary, moduleName, rgctxsDefDictionary);
                    if (Version < 108 && codeGenModule.rgctxsCount > 0)
                    {
                        var rgctxs = MapVATR<Il2CppRGCTXDefinition>(codeGenModule.rgctxs, codeGenModule.rgctxsCount);
                        var rgctxRanges = MapVATR<Il2CppTokenRangePair>(codeGenModule.rgctxRanges, codeGenModule.rgctxRangesCount);
                        foreach (var rgctxRange in rgctxRanges)
                        {
                            var rgctxDefs = new Il2CppRGCTXDefinition[rgctxRange.range.length];
                            Array.Copy(rgctxs, rgctxRange.range.start, rgctxDefs, 0, rgctxRange.range.length);
                            rgctxsDefDictionary[rgctxRange.token] = rgctxDefs;
                        }
                    }
                }
                if (Version >= 108 && metadata?.rgctxRanges != null && metadata.rgctxEntries != null)
                {
                    foreach (var imageDef in metadata.imageDefs)
                    {
                        var imageName = metadata.GetStringFromIndex(imageDef.nameIndex);
                        if (string.IsNullOrWhiteSpace(imageName))
                        {
                            continue;
                        }
                        if (!rgctxsDictionary.TryGetValue(imageName, out var rgctxsDefDictionary))
                        {
                            rgctxsDefDictionary = new Dictionary<uint, Il2CppRGCTXDefinition[]>();
                            rgctxsDictionary[imageName] = rgctxsDefDictionary;
                            AddNormalizedModule(normalizedRGCTXDataDictionary, imageName, rgctxsDefDictionary);
                        }
                        for (var i = 0; i < imageDef.rgctxRangesCount; i++)
                        {
                            var rgctxRangeIndex = imageDef.rgctxRangesStart + i;
                            if (rgctxRangeIndex < 0 || rgctxRangeIndex >= metadata.rgctxRanges.Length)
                            {
                                continue;
                            }
                            var rgctxRange = metadata.rgctxRanges[rgctxRangeIndex];
                            if (rgctxRange.range.start < 0 || rgctxRange.range.length < 0 ||
                                rgctxRange.range.start + rgctxRange.range.length > metadata.rgctxEntries.Length)
                            {
                                continue;
                            }
                            var rgctxDefs = new Il2CppRGCTXDefinition[rgctxRange.range.length];
                            Array.Copy(metadata.rgctxEntries, rgctxRange.range.start, rgctxDefs, 0, rgctxRange.range.length);
                            rgctxsDefDictionary[rgctxRange.token] = rgctxDefs;
                        }
                    }
                }
            }
            else
            {
                methodPointers = MapVATR<ulong>(pCodeRegistration.methodPointers, pCodeRegistration.methodPointersCount);
            }
            if (Version >= 108 && metadata != null)
            {
                var methodSpecCount = metadata.methodSpecsOnGenericType.Length + metadata.genericMethodSpecsOnType.Length + metadata.methodSpecs.Length;
                methodSpecs = new Il2CppMethodSpec[methodSpecCount];
                for (var i = 0; i < methodSpecs.Length; i++)
                {
                    methodSpecs[i] = metadata.GetMethodSpec(i);
                }
                var seenGenericMethodIndices = new HashSet<int>();
                foreach (var table in metadata.genericMethodFunctionsDefinitions)
                {
                    AddGenericMethodSpec(table.genericMethodIndex, table.indices.methodIndex, seenGenericMethodIndices);
                }
                foreach (var table in metadata.genericMethodFunctionsDefinitionsWithAdjustor)
                {
                    AddGenericMethodSpec(table.genericMethodIndex, table.methodIndex, seenGenericMethodIndices);
                }
            }
            else
            {
                genericMethodTable = MapVATR<Il2CppGenericMethodFunctionsDefinitions>(pMetadataRegistration.genericMethodTable, pMetadataRegistration.genericMethodTableCount);
                methodSpecs = MapVATR<Il2CppMethodSpec>(pMetadataRegistration.methodSpecs, pMetadataRegistration.methodSpecsCount);
                foreach (var table in genericMethodTable)
                {
                    AddGenericMethodSpec(table.genericMethodIndex, table.indices.methodIndex, null);
                }
            }
        }

        private void AddGenericMethodSpec(int genericMethodIndex, int methodPointerIndex, HashSet<int> seenGenericMethodIndices)
        {
            if (genericMethodIndex < 0 || genericMethodIndex >= methodSpecs.Length)
            {
                return;
            }
            var methodSpec = methodSpecs[genericMethodIndex];
            if (seenGenericMethodIndices == null || seenGenericMethodIndices.Add(genericMethodIndex))
            {
                var methodDefinitionIndex = methodSpec.methodDefinitionIndex;
                if (!methodDefinitionMethodSpecs.TryGetValue(methodDefinitionIndex, out var list))
                {
                    list = new List<Il2CppMethodSpec>();
                    methodDefinitionMethodSpecs.Add(methodDefinitionIndex, list);
                }
                list.Add(methodSpec);
            }
            methodSpecGenericMethodPointers[methodSpec] = methodPointerIndex >= 0 && methodPointerIndex < genericMethodPointers.Length
                ? genericMethodPointers[methodPointerIndex]
                : 0;
        }

        public T MapVATR<T>(ulong addr) where T : new()
        {
            return ReadClass<T>(MapVATR(addr));
        }

        public T[] MapVATR<T>(ulong addr, ulong count) where T : new()
        {
            return ReadClassArray<T>(MapVATR(addr), count);
        }

        public T[] MapVATR<T>(ulong addr, long count) where T : new()
        {
            return ReadClassArray<T>(MapVATR(addr), count);
        }

        public int GetFieldOffsetFromIndex(int typeIndex, int fieldIndexInType, int fieldIndex, bool isValueType, bool isStatic)
        {
            try
            {
                var offset = -1;
                if (fieldOffsetsArePointers)
                {
                    var ptr = fieldOffsets[typeIndex];
                    if (ptr > 0)
                    {
                        Position = MapVATR(ptr) + 4ul * (ulong)fieldIndexInType;
                        offset = ReadInt32();
                    }
                }
                else
                {
                    offset = (int)fieldOffsets[fieldIndex];
                }
                if (offset > 0)
                {
                    return offset;
                }
                else if (offset <= 0 && Metadata != null && typeIndex >= 0 && typeIndex < Metadata.typeDefs.Length)
                {
                    var typeDef = Metadata.typeDefs[typeIndex];
                    int runningOffset = 0;
                    if (!isStatic)
                    {
                        runningOffset = typeDef.IsValueType ? 0 : (Is32Bit ? 8 : 16);
                    }

                    for (int fIdx = 0; fIdx <= fieldIndexInType; fIdx++)
                    {
                        int currentGlobalFieldIdx = typeDef.fieldStart + fIdx;
                        if (currentGlobalFieldIdx >= Metadata.fieldDefs.Length)
                            break;

                        var fDef = Metadata.fieldDefs[currentGlobalFieldIdx];
                        var fType = types[fDef.typeIndex];

                        bool fIsStatic = (fType.attrs & 0x0010) != 0;
                        bool fIsLiteral = (fType.attrs & 0x0040) != 0;
                        if (isStatic)
                        {
                            if (!fIsStatic || fIsLiteral)
                            {
                                if (fIdx == fieldIndexInType)
                                {
                                    return 0;
                                }
                                continue;
                            }
                        }
                        else
                        {
                            if (fIsStatic || fIsLiteral)
                            {
                                if (fIdx == fieldIndexInType)
                                {
                                    return 0;
                                }
                                continue;
                            }
                        }

                        GetFieldSizeAndAlignment(fType, out int fSize, out int fAlign);

                        if (fAlign > 0)
                        {
                            int remainder = runningOffset % fAlign;
                            if (remainder != 0)
                            {
                                runningOffset += (fAlign - remainder);
                            }
                        }

                        if (fIdx == fieldIndexInType)
                        {
                            return runningOffset;
                        }

                        runningOffset += fSize;
                    }
                }
                return offset;
            }
            catch
            {
                return -1;
            }
        }

        private void GetFieldSizeAndAlignment(Il2CppType fieldType, out int size, out int align)
        {
            int ptrSize = Is32Bit ? 4 : 8;

            switch (fieldType.type)
            {
                case Il2CppTypeEnum.IL2CPP_TYPE_VOID:
                    size = 0;
                    align = 1;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                    size = 1;
                    align = 1;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                    size = 2;
                    align = 2;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                case Il2CppTypeEnum.IL2CPP_TYPE_R4:
                    size = 4;
                    align = 4;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                case Il2CppTypeEnum.IL2CPP_TYPE_R8:
                    size = 8;
                    align = 8;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_I:
                case Il2CppTypeEnum.IL2CPP_TYPE_U:
                case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                case Il2CppTypeEnum.IL2CPP_TYPE_BYREF:
                case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                case Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                case Il2CppTypeEnum.IL2CPP_TYPE_FNPTR:
                case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
                case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
                    size = ptrSize;
                    align = ptrSize;
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                    if (fieldType.valuetype == 1)
                    {
                        size = ptrSize * 2;
                        align = ptrSize;
                    }
                    else
                    {
                        size = ptrSize;
                        align = ptrSize;
                    }
                    break;
                case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                    long structTypeIndex = fieldType.data.klassIndex;
                    if (Metadata != null && structTypeIndex >= 0 && structTypeIndex < Metadata.typeDefs.Length)
                    {
                        GetStructSizeAndAlignment(structTypeIndex, out size, out align);
                    }
                    else
                    {
                        size = ptrSize * 2;
                        align = ptrSize;
                    }
                    break;
                default:
                    size = ptrSize;
                    align = ptrSize;
                    break;
            }
        }

        private void GetStructSizeAndAlignment(long typeIndex, out int size, out int align)
        {
            size = 0;
            align = 1;
            int ptrSize = Is32Bit ? 4 : 8;

            if (Metadata == null || typeIndex < 0 || typeIndex >= Metadata.typeDefs.Length)
            {
                size = ptrSize * 2;
                align = ptrSize;
                return;
            }

            var typeDef = Metadata.typeDefs[typeIndex];

            if (typeDef.IsEnum)
            {
                for (int i = 0; i < typeDef.field_count; i++)
                {
                    int currentGlobalFieldIdx = typeDef.fieldStart + i;
                    if (currentGlobalFieldIdx >= Metadata.fieldDefs.Length)
                        break;
                    var fDef = Metadata.fieldDefs[currentGlobalFieldIdx];
                    var fType = types[fDef.typeIndex];
                    bool fIsStatic = (fType.attrs & 0x0010) != 0;
                    if (!fIsStatic)
                    {
                        GetFieldSizeAndAlignment(fType, out size, out align);
                        return;
                    }
                }
                size = 4;
                align = 4;
                return;
            }

            int runningOffset = 0;
            int maxAlign = 1;

            for (int i = 0; i < typeDef.field_count; i++)
            {
                int currentGlobalFieldIdx = typeDef.fieldStart + i;
                if (currentGlobalFieldIdx >= Metadata.fieldDefs.Length)
                    break;
                var fDef = Metadata.fieldDefs[currentGlobalFieldIdx];
                var fType = types[fDef.typeIndex];
                bool fIsStatic = (fType.attrs & 0x0010) != 0;
                bool fIsLiteral = (fType.attrs & 0x0040) != 0;
                if (fIsStatic || fIsLiteral)
                    continue;

                GetFieldSizeAndAlignment(fType, out int fSize, out int fAlign);

                if (fAlign > maxAlign)
                    maxAlign = fAlign;

                int remainder = runningOffset % fAlign;
                if (remainder != 0)
                {
                    runningOffset += (fAlign - remainder);
                }

                runningOffset += fSize;
            }

            int finalRemainder = runningOffset % maxAlign;
            if (finalRemainder != 0)
            {
                runningOffset += (maxAlign - finalRemainder);
            }

            size = runningOffset;
            align = maxAlign;
        }

        public Il2CppType GetIl2CppType(ulong pointer)
        {
            if (!typeDic.TryGetValue(pointer, out var type))
            {
                return null;
            }
            return type;
        }

        public bool TryGetCodeGenModule(string imageName, out Il2CppCodeGenModule codeGenModule)
        {
            codeGenModule = null;
            if (codeGenModules != null && codeGenModules.TryGetValue(imageName, out codeGenModule))
            {
                return true;
            }
            return normalizedCodeGenModules != null && normalizedCodeGenModules.TryGetValue(NormalizeModuleName(imageName), out codeGenModule);
        }

        public bool TryGetRGCTXDataDictionary(string imageName, out Dictionary<uint, Il2CppRGCTXDefinition[]> rgctxs)
        {
            rgctxs = null;
            if (rgctxsDictionary != null && rgctxsDictionary.TryGetValue(imageName, out rgctxs))
            {
                return true;
            }
            return normalizedRGCTXDataDictionary != null && normalizedRGCTXDataDictionary.TryGetValue(NormalizeModuleName(imageName), out rgctxs);
        }

        public ulong GetMethodPointer(string imageName, Il2CppMethodDefinition methodDef)
        {
            if (Version >= 24.2)
            {
                if (methodDef.methodIndex < 0)
                {
                    return 0;
                }
                var methodToken = methodDef.token;
                if (!TryGetModuleMethodPointers(imageName, out var ptrs))
                {
                    return 0;
                }
                var methodPointerIndex = methodToken & 0x00FFFFFFu;
                if (methodPointerIndex == 0 || methodPointerIndex > ptrs.Length)
                {
                    return 0;
                }
                return ptrs[methodPointerIndex - 1];
            }
            else
            {
                var methodIndex = methodDef.methodIndex;
                if (methodIndex >= 0 && methodIndex < methodPointers.Length)
                {
                    return methodPointers[methodIndex];
                }
            }
            return 0;
        }

        private bool TryGetModuleMethodPointers(string imageName, out ulong[] ptrs)
        {
            ptrs = null;
            if (codeGenModuleMethodPointers != null && codeGenModuleMethodPointers.TryGetValue(imageName, out ptrs))
            {
                return true;
            }
            return normalizedCodeGenModuleMethodPointers != null && normalizedCodeGenModuleMethodPointers.TryGetValue(NormalizeModuleName(imageName), out ptrs);
        }

        private bool TryMapVATR(ulong addr, out ulong offset)
        {
            try
            {
                offset = MapVATR(addr);
                return true;
            }
            catch
            {
                offset = 0;
                return false;
            }
        }

        private static void AddNormalizedModule<T>(Dictionary<string, T> dictionary, string moduleName, T value)
        {
            var normalizedName = NormalizeModuleName(moduleName);
            if (!string.IsNullOrEmpty(normalizedName) && !dictionary.ContainsKey(normalizedName))
            {
                dictionary.Add(normalizedName, value);
            }
        }

        private static string NormalizeModuleName(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                return string.Empty;
            }
            var name = moduleName.Replace('\\', '/');
            var slashIndex = name.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                name = name[(slashIndex + 1)..];
            }
            if (name.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".cpp", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".c", System.StringComparison.OrdinalIgnoreCase))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }
            const string codeGenSuffix = "_CodeGen";
            if (name.EndsWith(codeGenSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^codeGenSuffix.Length];
            }
            return name;
        }

        public virtual ulong GetRVA(ulong pointer)
        {
            return pointer;
        }
    }
}