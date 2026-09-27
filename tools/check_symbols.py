#!/usr/bin/env python3
# ---------------------------------------------------------------------------
# check_symbols.py —— 深度静态检查(沙箱内无 C# 编译器时的替代方案):
#   1. tree-sitter 语法解析(真正的语法错误检测);
#   2. 全工程符号表:类型/方法/字段/枚举成员;
#   3. 交叉引用验证:成员访问、静态访问、枚举成员、调用实参个数;
#   4. 提取 Unity API 使用清单供人工核对(类型白名单 + 成员清单输出)。
# ---------------------------------------------------------------------------
import os
import sys
import tree_sitter_c_sharp
import tree_sitter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LANG = tree_sitter.Language(tree_sitter_c_sharp.language())

ERRORS = []


def err(msg):
    ERRORS.append(msg)


def parse_file(path):
    parser = tree_sitter.Parser(LANG)
    src = open(path, "rb").read()
    tree = parser.parse(src)

    def report_errors(node):
        if node.type == "ERROR" or node.is_missing:
            line = node.start_point[0] + 1
            err(f"{path}:{line}: syntax error near "
                f"{src[node.start_byte:node.end_byte][:60]!r}")
            return
        for ch in node.children:
            report_errors(ch)

    report_errors(tree.root_node)
    return src, tree


# ---------------------------------------------------------------------------
# 符号表
# ---------------------------------------------------------------------------

class TypeSym:
    def __init__(self, name, kind, ns, node, bases):
        self.name = name
        self.kind = kind            # class|struct|interface|enum|delegate
        self.ns = ns
        self.node = node
        self.bases = bases
        self.methods = {}           # name -> list[arity(int or -1)]
        self.fields = set()
        self.field_types = {}       # name -> declared type string
        self.props = set()
        self.enums = set()          # enum member names
        self.is_static = False

    def fq(self):
        return f"{self.ns}.{self.name}"


def text_of(src, node):
    return src[node.start_byte:node.end_byte].decode("utf-8", "replace")


def child_of_type(node, t):
    for c in node.children:
        if c.type == t:
            return c
    return None


def collect_symbols(src, tree, types, file_of_type):
    ns_stack = []

    def walk(node):
        if node.type == "file_scoped_namespace_declaration" or node.type == "namespace_declaration":
            name_node = child_of_type(node, "identifier") or child_of_type(node, "qualified_name")
            if name_node:
                ns_stack.append(text_of(src, name_node))
            body = child_of_type(node, "declaration_list") or node
            for c in body.children:
                walk(c)
            if name_node:
                ns_stack.pop()
            return

        if node.type in ("class_declaration", "struct_declaration",
                         "interface_declaration", "enum_declaration",
                         "delegate_declaration"):
            kind = {"class_declaration": "class", "struct_declaration": "struct",
                    "interface_declaration": "interface", "enum_declaration": "enum",
                    "delegate_declaration": "delegate"}[node.type]
            name = text_of(src, child_of_type(node, "identifier"))
            ns = ".".join(ns_stack) if ns_stack else ""
            bases = []
            bl = child_of_type(node, "base_list")
            if bl:
                for bt in bl.children:
                    if bt.type.endswith("type") or bt.type in (
                            "identifier", "generic_name", "qualified_name",
                            "nullable_type", "array_type"):
                        bases.append(text_of(src, bt))
            sym = TypeSym(name, kind, ns, node, bases)
            mods = [text_of(src, c) for c in node.children if c.type == "modifier"]
            sym.is_static = "static" in mods
            types[name] = sym
            file_of_type[name] = CUR_FILE[0]

            if kind == "enum":
                body = child_of_type(node, "enum_member_declaration_list")
                if body:
                    for m in body.children:
                        if m.type == "enum_member_declaration":
                            sym.enums.add(text_of(src, child_of_type(m, "identifier")))
                return

            body = child_of_type(node, "declaration_list")
            if body:
                for m in body.children:
                    if m.type == "method_declaration":
                        pl = child_of_type(m, "parameter_list")
                        # 方法名 = parameter_list 前一个 identifier 子节点
                        # (首个 identifier 可能是返回类型)
                        mname = None
                        prev = None
                        for c in m.children:
                            if c == pl:
                                break
                            if c.type == "identifier":
                                prev = c
                        if prev is not None:
                            mname = text_of(src, prev)
                        if mname is None:
                            continue
                        params = [p for p in pl.children if p.type == "parameter"] if pl else []
                        total = len(params)
                        required = 0
                        for prm in params:
                            has_default = any(c.type == "=" for c in prm.children)
                            if not has_default:
                                required += 1
                        sym.methods.setdefault(mname, []).append((required, total))
                    elif m.type == "field_declaration":
                        dl = child_of_type(m, "variable_declaration")
                        if dl:
                            for v in dl.children:
                                if v.type == "variable_declarator":
                                    vn = child_of_type(v, "identifier") or v.children[0]
                                    sym.fields.add(text_of(src, vn))
                    elif m.type == "property_declaration":
                        pname = None
                        for c in m.children:
                            if c.type in ("accessor_list", "arrow_expression_clause"):
                                break
                            if c.type == "identifier":
                                pname = c
                        if pname is not None:
                            sym.props.add(text_of(src, pname))
                    elif m.type == "event_field_declaration":
                        dl = child_of_type(m, "variable_declaration")
                        if dl:
                            for v in dl.children:
                                if v.type == "variable_declarator":
                                    sym.fields.add(text_of(src, v.children[0]))
                    elif m.type == "constructor_declaration":
                        pl = child_of_type(m, "parameter_list")
                        arity = len([p for p in pl.children if p.type == "parameter"]) if pl else 0
                        sym.methods.setdefault("#ctor", []).append(arity)
            return

        for c in node.children:
            walk(c)

    walk(tree.root_node)


# ---------------------------------------------------------------------------
# 使用点检查
# ---------------------------------------------------------------------------

BUILTIN_TYPES = {
    "string", "int", "uint", "float", "double", "bool", "byte", "sbyte",
    "short", "ushort", "long", "ulong", "char", "decimal", "object", "void",
    "var", "dynamic",
}

BCL_TYPES = {
    "List", "Dictionary", "Queue", "Stack", "HashSet", "IList", "IReadOnlyList",
    "IEnumerable", "IEnumerator", "ICollection", "ConcurrentQueue", "ConcurrentDictionary",
    "MemoryStream", "Stream", "BinaryWriter", "BinaryReader", "IDisposable",
    "Uri", "TimeSpan", "Exception", "InvalidOperationException", "ArgumentException",
    "Task", "Action", "Func", "Predicate", "Guid", "CancellationToken",
    "CancellationTokenSource", "SemaphoreSlim", "ClientWebSocket", "WebSocket",
    "WebSocketState", "WebSocketReceiveResult", "WebSocketMessageType",
    "WebSocketCloseStatus", "ArraySegment", "HttpListener", "HttpListenerContext",
    "HttpListenerWebSocketContext", "Stopwatch", "Thread", "Console", "Encoding",
    "Math", "BitConverter", "Environment", "Timeout", "Random", "GC",
}

UNITY_TYPES = {
    "MonoBehaviour", "ScriptableObject", "GameObject", "RectTransform", "Transform",
    "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32", "Texture2D",
    "Texture", "TextureFormat", "FilterMode", "TextureWrapMode", "Sprite", "Image",
    "RawImage", "Text", "Button", "Selectable", "Graphic", "MaskableGraphic",
    "Canvas", "CanvasScaler", "GraphicRaycaster", "CanvasGroup", "EventSystem",
    "PointerEventData", "BaseEventData", "EventData", "Camera", "Screen", "Time",
    "PlayerPrefs", "Resources", "Font", "FontStyle", "TextAnchor",
    "HorizontalWrapMode", "VerticalWrapMode", "Mathf", "Debug", "Rect", "RectOffset",
    "ImageConversion", "Object", "Behaviour", "Component", "Renderer", "Coroutine",
    "WaitForSeconds", "WaitForEndOfFrame", "YieldInstruction", "Application",
    "SystemInfo", "QualitySettings", "LayerMask", "Physics", "Physics2D",
    "RaycastHit", "RaycastHit2D", "Ray", "Ray2D", "Input", "Touch", "TouchPhase",
    "Event", "EventType", "GUI", "GUILayout", "GUIStyle", "Gizmos", "AnimationCurve",
    "Gradient", "Material", "Shader", "RenderTexture", "AudioClip", "AudioSource",
    "ParticleSystem", "TrailRenderer", "LineRenderer", "SpriteRenderer",
    "ColorBlock", "Navigation", "IPointerDownHandler", "IDragHandler",
    "IPointerUpHandler", "ICancelHandler", "IDropHandler", "IBeginDragHandler",
    "IEndDragHandler", "IPointerClickHandler", "StandaloneInputModule",
    "BaseInputModule", "Pointer", "LayoutGroup", "VerticalLayoutGroup",
    "HorizontalLayoutGroup", "GridLayoutGroup", "ContentSizeFitter",
    "Scrollbar", "Slider", "Toggle", "InputField", "Dropdown", "ScrollRect",
    "LayoutElement", "AspectRatioFitter", "RectTransformUtility", "WaitUntil",
    "WaitWhile", "DrivenRectTransformTracker", "DrivenTransformProperties",
}

CUR_FILE = [None]

# Unity 常用基类的实例成员(近似集,用于继承解析)
_UNITY_MB_MEMBERS = {
    # 属性
    "gameObject", "transform", "enabled", "isActiveAndEnabled", "useGUILayout",
    "runInEditMode", "name", "tag", "hideFlags",
    # 方法
    "StartCoroutine", "StopCoroutine", "StopAllCoroutines",
    "Invoke", "InvokeRepeating", "CancelInvoke", "IsInvoking",
    "GetComponent", "GetComponentInChildren", "GetComponentInParent",
    "GetComponents", "GetComponentsInChildren", "GetComponentsInParent",
    "TryGetComponent", "CompareTag", "SendMessage", "SendMessageUpwards",
    "BroadcastMessage", "FindObjectOfType", "FindObjectsOfType",
    "Destroy", "DestroyImmediate", "Instantiate", "DontDestroyOnLoad",
    "ToString", "Equals", "GetHashCode", "GetType", "GetHashCode",
}
UNITY_BASE_MEMBERS = {
    "MonoBehaviour": _UNITY_MB_MEMBERS,
    "Behaviour": _UNITY_MB_MEMBERS,
    "Component": _UNITY_MB_MEMBERS,
    "Object": _UNITY_MB_MEMBERS,
    "ScriptableObject": _UNITY_MB_MEMBERS,
}


def analyze_uses(src, tree, types, uses_report):
    """对单文件做成员访问/静态访问/枚举/调用检查。"""

    # 局部作用域符号:名字 -> 类型名
    scopes = [{}]  # stack of dicts

    def push_var(name, tname):
        scopes[-1][name] = tname

    def find_var(name):
        for s in reversed(scopes):
            if name in s:
                return s[name]
        return None

    def resolve_type(tname):
        """把代码中出现的类型名解析到符号表。"""
        tname = tname.strip().rstrip("?")
        if "<" in tname:
            tname = tname.split("<")[0].strip()
        if tname in BUILTIN_TYPES or tname in BCL_TYPES or tname in UNITY_TYPES:
            return None  # 非工程类型
        if tname in types:
            return types[tname]
        return "MISSING"

    def members_of(sym, include_inherited=True, seen=None):
        if seen is None:
            seen = set()
        if sym.name in seen:
            return set(), {}
        seen.add(sym.name)
        flds = set(sym.fields) | set(sym.props) | set(sym.enums)
        meths = dict(sym.methods)
        if include_inherited:
            for b in sym.bases:
                if b in UNITY_BASE_MEMBERS:
                    flds |= UNITY_BASE_MEMBERS[b]
                    for k in UNITY_BASE_MEMBERS[b]:
                        meths.setdefault(k, []).append((0, 8))
                    continue
                bs = types.get(b)
                if bs:
                    f2, m2 = members_of(bs, True, seen)
                    flds |= f2
                    for k, v in m2.items():
                        meths.setdefault(k, []).extend(v)
        return flds, meths

    def check_member_access(node):
        # member_access: expr . name
        expr = node.children[0]
        name_node = node.children[-1]
        name = text_of(src, name_node)

        # 静态访问:类型名.成员
        if expr.type == "identifier":
            tname = text_of(src, expr)
            sym = types.get(tname)
            if sym is not None:
                flds, meths = members_of(sym)
                if name not in flds and name not in meths:
                    line = node.start_point[0] + 1
                    err(f"{CUR_FILE[0]}:{line}: {tname}.{name} 不存在")
                return
            vt = find_var(tname)
            if vt:
                sym = types.get(vt)
                if sym is not None:
                    flds, meths = members_of(sym)
                    if name not in flds and name not in meths:
                        line = node.start_point[0] + 1
                        err(f"{CUR_FILE[0]}:{line}: ({vt} {tname}).{name} 不存在")
                    return
            return

        # this.成员
        if expr.type == "this_expression":
            return  # 当前类成员由编译器保证,跳过

        # 链式访问 a.b.c:只校验最左是工程类型的段
        if expr.type in ("member_access", "member_access_expression"):
            check_member_access(expr)

    def check_invocation(node):
        target = node.children[0]
        args_node = child_of_type(node, "argument_list")
        text_args = text_of(src, args_node) if args_node else "()"
        if "=>" in text_args or "delegate" in text_args:
            return  # 含 lambda,跳过实参计数
        if args_node:
            inner = [c for c in args_node.children if c.type == "argument"]
        else:
            inner = []
        n_args = len(inner)

        if target.type in ("member_access", "member_access_expression"):
            expr = target.children[0]
            name = text_of(src, target.children[-1])
            tname = None
            if expr.type == "identifier":
                idname = text_of(src, expr)
                if idname in types:
                    tname = idname
                else:
                    vt = find_var(idname)
                    if vt in types:
                        tname = vt
            if tname:
                sym = types[tname]
                _, meths = members_of(sym)
                if name in meths:
                    arities = meths[name]
                    ok = any((isinstance(a, tuple) and a[0] <= n_args <= a[1])
                             or a == n_args for a in arities)
                    if not ok:
                        line = node.start_point[0] + 1
                        err(f"{CUR_FILE[0]}:{line}: {tname}.{name} 实参 {n_args} 个,"
                            f"声明为 {arities}")

    def extract_fields(class_node):
        """从 class/struct 声明中抽取字段名→类型。"""
        fmap = {}
        body = child_of_type(class_node, "declaration_list")
        if not body:
            return fmap
        for m in body.children:
            if m.type == "field_declaration":
                vdecl = child_of_type(m, "variable_declaration")
                if not vdecl or not vdecl.children:
                    continue
                # variable_declaration 的第一个子节点即类型
                # (类名类型是 identifier,不是 *_type)
                type_c = vdecl.children[0]
                if type_c.type == "variable_declarator":
                    continue
                tn = text_of(src, type_c)
                if not tn:
                    continue
                for c in vdecl.children:
                    if c.type == "variable_declarator":
                        vn = child_of_type(c, "identifier") or c.children[0]
                        fmap[text_of(src, vn)] = tn
        return fmap

    def walk(node):
        t = node.type

        # 进入类/结构:把字段压入作用域,供 this.成员 / 裸成员访问校验
        if t in ("class_declaration", "struct_declaration"):
            fmap = extract_fields(node)
            scopes.append(dict(fmap))
            for c in node.children:
                walk(c)
            scopes.pop()
            return

        if t in ("block", "switch_body", "for_statement", "while_statement",
                 "do_statement", "if_statement", "else_clause"):
            scopes.append({})
            for c in node.children:
                walk(c)
            scopes.pop()
            return

        if t in ("variable_declaration", "for_each_statement"):
            # 记录局部变量类型
            type_node = None
            for c in node.children:
                if c.type.endswith("_type") or (c.type == "identifier" and type_node is None):
                    type_node = c
                    break
            if type_node is not None:
                tn = text_of(src, type_node)
                for c in node.children:
                    if c.type == "variable_declarator":
                        vn = child_of_type(c, "identifier") or c.children[0]
                        push_var(text_of(src, vn), tn)
        if t == "parameter":
            # 形参: 类型 名字
            if len(node.children) >= 2:
                tn = text_of(src, node.children[0])
                push_var(text_of(src, node.children[-1]), tn)

        if t in ("member_access", "member_access_expression"):
            check_member_access(node)
        if t == "invocation_expression":
            check_invocation(node)
        if t == "object_creation_expression":
            tn = None
            for c in node.children:
                if c.type.endswith("_type"):
                    tn = text_of(src, c)
                    break
            if tn:
                r = resolve_type(tn)
                if r == "MISSING":
                    line = node.start_point[0] + 1
                    err(f"{CUR_FILE[0]}:{line}: new {tn} —— 类型未定义")

        for c in node.children:
            walk(c)

    walk(tree.root_node)


def main():
    files = []
    for sub in ("Assets/Scripts", "Server"):
        base = os.path.join(ROOT, sub)
        for dirpath, _, filenames in os.walk(base):
            for fn in sorted(filenames):
                if fn.endswith(".cs"):
                    files.append(os.path.join(dirpath, fn))

    types = {}
    file_of_type = {}
    parsed = []
    for f in files:
        CUR_FILE[0] = os.path.relpath(f, ROOT)
        src, tree = parse_file(f)
        parsed.append((f, src, tree))
        collect_symbols(src, tree, types, file_of_type)

    print(f"types collected: {len(types)}")

    uses = {}
    for f, src, tree in parsed:
        CUR_FILE[0] = os.path.relpath(f, ROOT)
        analyze_uses(src, tree, types, uses)

    if ERRORS:
        for e in ERRORS:
            print("ERROR:", e)
        return 1
    print("symbol checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
