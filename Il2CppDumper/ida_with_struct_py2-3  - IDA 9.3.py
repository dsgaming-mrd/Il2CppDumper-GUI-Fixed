import json
import idaapi
import idc
import ida_typeinf
import ida_funcs
import ida_bytes

processFields = [
    "ScriptMethod",
    "ScriptString",
    "ScriptMetadata",
    "ScriptMetadataMethod",
    "Addresses",
]

imageBase = idaapi.get_imagebase()

def to_ea(addr):
    if addr is None:
        return idc.BADADDR
    if isinstance(addr, str):
        addr = int(addr, 16) if addr.startswith(('0x', '0X')) else int(addr)
    return int(addr) & 0xFFFFFFFFFFFFFFFF

def get_addr(addr):
    val = to_ea(addr)
    if val == idc.BADADDR:
        return idc.BADADDR
    return (imageBase + val) & 0xFFFFFFFFFFFFFFFF

def set_name(addr, name):
    addr = to_ea(addr)
    if addr == idc.BADADDR or not ida_bytes.is_mapped(addr):
        return
    ret = idc.set_name(addr, name, idc.SN_NOWARN | idc.SN_NOCHECK)
    if ret == 0:
        new_name = f"{name}_{addr:X}"
        idc.set_name(addr, new_name, idc.SN_NOWARN | idc.SN_NOCHECK)

def make_function(start, end):
    start = to_ea(start)
    end = to_ea(end)

    if start == idc.BADADDR or end == idc.BADADDR:
        return
    if start == 0 or start >= end:
        return
    if not ida_bytes.is_mapped(start):
        return

    try:
        p_fn = ida_funcs.get_next_func(start)
        if p_fn:
            next_ea = p_fn.start_ea & 0xFFFFFFFFFFFFFFFF
            if next_ea < end:
                end = next_ea
    except Exception:
        pass

    try:
        if idc.get_func_attr(start, idc.FUNCATTR_START) == start:
            ida_funcs.del_func(start)
        ida_funcs.add_func(start, end)
    except Exception:
        pass

def apply_signature_9x(addr, signature):
    addr = to_ea(addr)
    if addr == idc.BADADDR or not ida_bytes.is_mapped(addr):
        return False
    tif = ida_typeinf.tinfo_t()
    try:
        name, tp, fld = ida_typeinf.parse_decl(None, signature, 0)
        if tp is not None:
            if tif.deserialize(None, tp, fld):
                if ida_typeinf.apply_tinfo(addr, tif, ida_typeinf.TINFO_DEFINITE):
                    return True
    except Exception:
        pass
    return False

path = idaapi.ask_file(False, '*.json', 'script.json from Il2cppdumper')
if not path:
    print("No script.json selected.")
    exit(0)

hpath = idaapi.ask_file(False, '*.h', 'il2cpp.h from Il2cppdumper')
if hpath:
    with open(hpath, 'r', encoding='utf-8', errors='ignore') as f:
        idc.parse_decls(f.read(), 0)

with open(path, 'rb') as f:
    data = json.loads(f.read().decode('utf-8'))

# 1. Addresses
if "Addresses" in data and "Addresses" in processFields:
    addresses = data["Addresses"]
    total = len(addresses)
    print(f"Processing {total} addresses...")
    for index in range(total - 1):
        start = get_addr(addresses[index])
        end = get_addr(addresses[index + 1])
        make_function(start, end)

# 2. ScriptMethod
if "ScriptMethod" in data and "ScriptMethod" in processFields:
    scriptMethods = data["ScriptMethod"]
    print(f"Processing {len(scriptMethods)} methods...")
    for scriptMethod in scriptMethods:
        addr = get_addr(scriptMethod["Address"])
        name = scriptMethod["Name"]
        set_name(addr, name)
        signature = scriptMethod.get("Signature")
        if signature:
            apply_signature_9x(addr, signature)

# 3. ScriptString
if "ScriptString" in data and "ScriptString" in processFields:
    scriptStrings = data["ScriptString"]
    print(f"Processing {len(scriptStrings)} string literals...")
    for index, scriptString in enumerate(scriptStrings, start=1):
        addr = get_addr(scriptString["Address"])
        if addr != idc.BADADDR and ida_bytes.is_mapped(addr):
            value = scriptString["Value"]
            name = f"StringLiteral_{index}"
            idc.set_name(addr, name, idc.SN_NOWARN)
            idc.set_cmt(addr, value, 1)

# 4. ScriptMetadata
if "ScriptMetadata" in data and "ScriptMetadata" in processFields:
    scriptMetadatas = data["ScriptMetadata"]
    for scriptMetadata in scriptMetadatas:
        addr = get_addr(scriptMetadata["Address"])
        name = scriptMetadata["Name"]
        set_name(addr, name)
        idc.set_cmt(addr, name, 1)
        signature = scriptMetadata.get("Signature")
        if signature:
            apply_signature_9x(addr, signature)

# 5. ScriptMetadataMethod
if "ScriptMetadataMethod" in data and "ScriptMetadataMethod" in processFields:
    scriptMetadataMethods = data["ScriptMetadataMethod"]
    for scriptMetadataMethod in scriptMetadataMethods:
        addr = get_addr(scriptMetadataMethod["Address"])
        name = scriptMetadataMethod["Name"]
        methodAddr = get_addr(scriptMetadataMethod["MethodAddress"])
        set_name(addr, name)
        idc.set_cmt(addr, name, 1)
        if methodAddr != idc.BADADDR:
            idc.set_cmt(addr, f"{methodAddr:X}", 0)

print('Script finished successfully!')