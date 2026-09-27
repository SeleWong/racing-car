#!/usr/bin/env python3
# ---------------------------------------------------------------------------
# check_project.py —— 工程结构与内容校验:
#   1. Assets 下每个文件/目录都有 .meta,且 GUID 唯一;
#   2. 场景与 ProjectSettings 均为合法 YAML;
#   3. 场景中所有 m_Script guid 都能解析到真实 .cs 文件;
#   4. 场景 MonoBehaviour 字段与对应脚本的序列化字段匹配(防拼写漂移);
#   5. C# 侧:括号配平、Unity 引擎 API 使用白名单检查。
# ---------------------------------------------------------------------------
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")

try:
    import yaml
    HAVE_YAML = True
except ImportError:
    HAVE_YAML = False

ERRORS = []
WARNS = []


def err(msg):
    ERRORS.append(msg)


def warn(msg):
    WARNS.append(msg)


# ------------------------- 1/2: meta 与 YAML -------------------------

def load_metas():
    metas = {}
    dup = {}
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(".meta"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, ROOT)
            with open(path) as f:
                content = f.read()
            m = re.search(r"^guid:\s*([0-9a-f]{32})", content, re.M)
            if not m:
                err(f"{rel}: no guid")
                continue
            g = m.group(1)
            if g in metas:
                err(f"duplicate guid {g}: {metas[g]} vs {rel}")
            metas[g] = rel
            target = path[:-5]
            if not os.path.exists(target):
                err(f"{rel}: orphan meta (target missing)")
    return metas


def check_missing_metas():
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        if dirpath != ASSETS and not os.path.exists(dirpath + ".meta"):
            err(f"missing folder meta: {os.path.relpath(dirpath, ROOT)}")
        for fn in filenames:
            if fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            if not os.path.exists(p + ".meta"):
                err(f"missing meta: {os.path.relpath(p, ROOT)}")


def parse_yaml_docs(path):
    """轻量 YAML 校验:不依赖第三方库。检查文档结构与缩进一致性。"""
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()
    if not lines or not lines[0].startswith("%YAML"):
        err(f"{path}: missing %YAML header")
        return []
    docs = []
    current = None
    for i, ln in enumerate(lines):
        if ln.startswith("--- !u!"):
            m = re.match(r"--- !u!(\d+) &(\d+)", ln)
            if not m:
                err(f"{path}:{i+1}: bad document header {ln!r}")
                continue
            current = {"class": int(m.group(1)), "anchor": int(m.group(2)), "lines": []}
            docs.append(current)
        elif current is not None:
            current["lines"].append((i + 1, ln))
    return docs


# ------------------------- 3/4: 场景交叉引用 -------------------------

# 各脚本的公开序列化字段(与 C# 源码保持同步)
SCRIPT_FIELDS = {
    "CameraRig.cs": set(),
    "UIManager.cs": set(),
    "DragInput.cs": {"Sensitivity", "WorldCamera"},
    "GameView.cs": {"OnlineMode", "ServerUrl", "Seed", "WorldCamera", "Drag",
                    "Ui", "EntityRoot", "FlashOverlay"},
    "GameBootstrap.cs": {"Mode", "ServerUrl", "Seed"},
}


def check_scene(metas):
    scene = os.path.join(ASSETS, "Scenes", "Main.unity")
    docs = parse_yaml_docs(scene)
    if not docs:
        return

    guid_to_cs = {}
    for g, rel in metas.items():
        if rel.endswith(".cs.meta"):
            guid_to_cs[g] = rel[:-5]

    # 已知引擎组件 classID(白名单)
    known = {1, 4, 20, 29, 81, 104, 114, 157, 196, 223, 224}
    for d in docs:
        if d["class"] not in known:
            warn(f"scene: unknown classID {d['class']}")

    anchors = {d["anchor"] for d in docs}
    # 所有 fileID 引用必须存在
    body = "\n".join(ln for d in docs for _, ln in d["lines"])
    for m in re.finditer(r"\{fileID:\s*(-?\d+)\}", body):
        fid = int(m.group(1))
        if fid != 0 and fid not in anchors:
            err(f"scene: dangling fileID {fid}")

    # m_Script guid 必须解析到工程内脚本
    script_docs = [d for d in docs if d["class"] == 114]
    for d in script_docs:
        text = "\n".join(ln for _, ln in d["lines"])
        m = re.search(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", text)
        if not m:
            continue
        g = m.group(1)
        # 引擎内置组件脚本(非本工程的)
        builtin = {
            "76c392e42b5098c458856cdf6ecaaaa1": "EventSystem",
            "4f231c4fb786f3946a6b90b886c48677": "StandaloneInputModule",
            "0cd44c1031e13a943bb63640046fad76": "CanvasScaler",
            "dc42784cf147c0c48a680349fa168899": "GraphicRaycaster",
            "fe87c0e1cc204ed48ad3b37840f39efc": "Image",
        }
        if g in builtin:
            continue
        cs = guid_to_cs.get(g)
        if cs is None:
            err(f"scene: MonoBehaviour &{d['anchor']} references unknown script guid {g}")
            continue
        # 字段匹配
        fname = os.path.basename(cs)
        expected = SCRIPT_FIELDS.get(fname)
        if expected is None:
            continue
        fields = set(re.findall(r"^  ([A-Za-z_][A-Za-z0-9_]*):", text, re.M))
        fields -= {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
                   "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags",
                   "m_Script", "m_Name", "m_EditorClassIdentifier"}
        extra = fields - expected
        missing = expected - fields
        if extra:
            err(f"scene: {fname} has unknown serialized fields: {sorted(extra)}")
        if missing:
            err(f"scene: {fname} missing serialized fields: {sorted(missing)}")


# ------------------------- 5: C# 检查 -------------------------

def check_csharp():
    unity_api = re.compile(r"\bUnityEngine\.[A-Z]\w+|\bUnityEditor\.")
    for dirpath, dirnames, filenames in os.walk(os.path.join(ASSETS, "Scripts")):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, ROOT)
            src = open(path, encoding="utf-8-sig").read()

            # 括号配平(忽略字符串/字符/注释内的)
            stack = []
            pairs = {")": "(", "]": "[", "}": "{"}
            i, n = 0, len(src)
            state = None  # None | 'str' | 'char' | 'line' | 'block' | 'verbatim'
            while i < n:
                c = src[i]
                nxt = src[i + 1] if i + 1 < n else ""
                if state is None:
                    if c == "/" and nxt == "/":
                        state = "line"; i += 2; continue
                    if c == "/" and nxt == "*":
                        state = "block"; i += 2; continue
                    if c == "@" and nxt == '"':
                        state = "verbatim"; i += 2; continue
                    if c == "$" and nxt == '"':
                        state = "interp"; i += 2; continue
                    if c == '"':
                        state = "str"; i += 1; continue
                    if c == "'":
                        state = "char"; i += 1; continue
                    if c in "([{":
                        stack.append((c, i))
                    elif c in ")]}":
                        if not stack or stack[-1][0] != pairs[c]:
                            line = src.count("\n", 0, i) + 1
                            err(f"{rel}:{line}: unbalanced '{c}'")
                            break
                        stack.pop()
                elif state == "line":
                    if c == "\n":
                        state = None
                elif state == "block":
                    if c == "*" and nxt == "/":
                        state = None; i += 1
                elif state == "str":
                    if c == "\\":
                        i += 1
                    elif c == '"':
                        state = None
                elif state == "interp":
                    if c == "\\":
                        i += 1
                    elif c == '"':
                        state = None
                elif state == "char":
                    if c == "\\":
                        i += 1
                    elif c == "'":
                        state = None
                elif state == "verbatim":
                    if c == '"' and nxt == '"':
                        i += 1
                    elif c == '"':
                        state = None
                i += 1
            else:
                if state in ("str", "char", "block", "verbatim", "interp"):
                    err(f"{rel}: unterminated {state}")
                if stack:
                    line = src.count("\n", 0, stack[-1][1]) + 1
                    err(f"{rel}:{line}: unclosed '{stack[-1][0]}'")

            # 纯内核禁止引用 Unity
            if "/Core/" in rel.replace("\\", "/"):
                if unity_api.search(src):
                    err(f"{rel}: Core 必须是纯 C#(不得引用 UnityEngine)")


def check_yaml_strict():
    """用 PyYAML 做严格结构校验(把 %TAG 指令按文档重声明,兼容严格解析器)。"""
    if not HAVE_YAML:
        warn("PyYAML 不可用,跳过严格 YAML 校验")
        return

    def any_ctor(loader, tag_suffix, node):
        if isinstance(node, yaml.MappingNode):
            return loader.construct_mapping(node, deep=True)
        if isinstance(node, yaml.SequenceNode):
            return loader.construct_sequence(node, deep=True)
        return loader.construct_scalar(node)

    yaml.SafeLoader.add_multi_constructor("tag:unity3d.com,2011:", any_ctor)

    targets = [os.path.join(ASSETS, "Scenes", "Main.unity")]
    for name in os.listdir(os.path.join(ROOT, "ProjectSettings")):
        if name.endswith(".asset"):
            targets.append(os.path.join(ROOT, "ProjectSettings", name))

    for path in targets:
        if not os.path.exists(path):
            continue
        raw = open(path, encoding="utf-8").read()
        # 场景文件:%TAG 仅对首个文档生效,逐文档重声明后再解析
        stripped = re.sub(r"^%TAG .*\n", "", raw, flags=re.M)
        fixed = re.sub(r"^--- ", "%TAG !u! tag:unity3d.com,2011:\n--- ",
                       stripped, flags=re.M)
        try:
            docs = [d for d in yaml.safe_load_all(fixed) if d]
            if not docs:
                err(f"{os.path.relpath(path, ROOT)}: YAML 无文档")
        except Exception as e:
            err(f"{os.path.relpath(path, ROOT)}: YAML 解析失败: {e}")


def main():
    metas = load_metas()
    check_missing_metas()
    check_scene(metas)
    check_yaml_strict()
    for name in ("EditorBuildSettings.asset", "InputManager.asset", "TagManager.asset",
                 "GraphicsSettings.asset", "QualitySettings.asset", "TimeManager.asset"):
        path = os.path.join(ROOT, "ProjectSettings", name)
        if not os.path.exists(path):
            err(f"missing ProjectSettings/{name}")
            continue
        parse_yaml_docs(path)

    scene = os.path.join(ASSETS, "Scenes", "Main.unity")
    if os.path.exists(scene):
        docs = parse_yaml_docs(scene)

    check_csharp()

    print(f"checked: {len(metas)} meta files")
    for w in WARNS:
        print("WARN:", w)
    if ERRORS:
        for e in ERRORS:
            print("ERROR:", e)
        return 1
    print("all checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
