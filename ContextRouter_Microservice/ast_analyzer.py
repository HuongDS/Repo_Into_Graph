import tree_sitter
import tree_sitter_c_sharp as tscsharp
import tree_sitter_java as tsjava
import tree_sitter_python as tspython

# Khởi tạo các parser
LANGUAGES = {
    "cs": tree_sitter.Language(tscsharp.language()),
    "java": tree_sitter.Language(tsjava.language()),
    "python": tree_sitter.Language(tspython.language()),
}

PARSERS = {}
for ext, lang in LANGUAGES.items():
    parser = tree_sitter.Parser(lang)
    PARSERS[ext] = parser

def analyze_ast(code: str, language_ext: str):
    """
    Phân tích AST để đếm V(G), LSLOC và lấy AST metadata.
    Trả về (is_valid, vg, lsloc, root_node_type)
    """
    ext = language_ext.lower()
    if ext not in PARSERS:
        # Mặc định C#
        ext = "cs"
        
    parser = PARSERS[ext]
    # Parsing code to AST
    tree = parser.parse(bytes(code, "utf8"))
    
    is_valid = not tree.root_node.has_error
    
    # Duyệt cây để đếm V(G) và LSLOC
    # V(G) = 1 + số node rẽ nhánh
    vg = 1
    lsloc = 0
    
    # Các loại node đếm LSLOC chuẩn hóa
    STATEMENT_TYPES = {
        'if_statement', 'for_statement', 'enhanced_for_statement', 'for_each_statement', 'foreach_statement',
        'while_statement', 'do_statement', 'switch_statement', 'switch_expression',
        'expression_statement', 'local_variable_declaration', 'local_declaration_statement',
        'field_declaration', 'method_declaration', 'constructor_declaration',
        'class_declaration', 'interface_declaration', 'enum_declaration',
        'return_statement', 'throw_statement', 'break_statement', 'continue_statement',
        'try_statement', 'catch_clause', 'except_clause', 'finally_clause', 'using_statement', 'lock_statement',
        'annotation', 'marker_annotation'
    }
    
    # Các loại node thường đại diện cho nhánh rẽ
    branch_nodes = {
        "if_statement", "while_statement", "do_statement", 
        "for_statement", "enhanced_for_statement", "foreach_statement",
        "catch_clause", "except_clause", 
        "switch_label", "switch_section", "case_clause",
        "conditional_expression", "ternary_expression",
        "elif_clause"
    }
    
    def traverse(node):
        nonlocal vg, lsloc
        t = node.type
        
        # Đếm LSLOC
        if t in STATEMENT_TYPES:
            lsloc += 1
            
        # Check node type cho V(G)
        if t in branch_nodes:
            # Nếu là nhánh switch, bỏ qua nhánh default
            is_default = False
            if t in ["switch_label", "switch_section", "case_clause"]:
                # Trong Java/C#, text của node default hoặc child của nó chứa 'default'
                for child in node.children:
                    if child.type == "default" or (child.text and child.text.decode("utf-8").startswith("default")):
                        is_default = True
                if node.text and node.text.decode("utf-8").startswith("default"):
                    is_default = True
                    
            if not is_default:
                vg += 1
            
        # Trong C# và Java, biểu thức nhị phân có && và || cũng làm tăng V(G)
        if t in ["binary_expression", "boolean_operator"]:
            # Tìm child không phải là named node (tức là dấu operator)
            for child in node.children:
                if not child.is_named and child.type in ["&&", "||", "and", "or", "??"]:
                    vg += 1
                    break

        for child in node.children:
            # Bỏ qua các variable_declaration bên trong local_declaration_statement để tránh đếm kép
            if t == 'local_declaration_statement' and child.type == 'variable_declaration':
                for grandchild in child.children:
                    traverse(grandchild)
                continue
            traverse(child)
            
    traverse(tree.root_node)
    
    root_node_type = tree.root_node.type if tree.root_node else "unknown"
    return is_valid, vg, lsloc, root_node_type
