## Unity Structure Tree Builder

`UnityStructureTreeBuilder` cung cấp một `ScriptableObject` (`StructureTree`) và custom editor để:

- **Export** cấu trúc thư mục hiện có trong Project thành một `StructureTree` asset (bao gồm cả `.asmdef`).
- **Tạo lại** cấu trúc thư mục + assembly definition từ một `StructureTree` asset.

### Cấu trúc chính

- `StructureTree.cs`
  - `rootPath` (`string`): đường dẫn thư mục cha (ví dụ: `Assets/Submodules`).
  - `root` (`StructureNode`): node gốc của cây cấu trúc.
  - `CreateStructure()`: tạo folders + `.asmdef` files từ cây config.
  - Menu **Assets/Export Structure Tree**: export folder hiện có thành `StructureTree` asset.

- `StructureNode`
  - `name`: tên thư mục.
  - `children`: danh sách node con (`List<StructureNode>`).
  - `asmdef` (`AsmdefConfig`, nullable): nếu có, tạo `.asmdef` trong folder này.

- `AsmdefConfig`
  - `assemblyName`: tên assembly.
  - `references`: danh sách tên các assembly phụ thuộc.

- `StructureTreeEditor.cs`
  - Custom inspector:
    - `Root Path` với nút browse folder (`...`).
    - Cây `StructureNode` với foldout, thêm/xóa node.
    - Toggle `ASM`/`+a` trên mỗi node để gắn/bỏ asmdef config.
    - Asmdef config: field Assembly name + References (dropdown chọn từ các asm trong config hoặc tự điền).
    - Nút `All`/`None` để expand/collapse toàn bộ tree.
    - Badge `(N)` hiện số children khi node collapsed.
    - Cảnh báo khi có trùng tên assembly.
    - Confirm dialog khi xóa node có children.
    - Nút **Create Structure** để tạo toàn bộ.

### Cách export từ Project

1. Trong Project window, chọn **một folder** (panel phải hoặc right-click cây bên trái).
2. Menu **Assets → Export Structure Tree**.
3. Chọn nơi lưu asset.
4. Asset sẽ capture:
   - Cây thư mục con.
   - `.asmdef` files nếu có (tên + references).

### Cách tạo cấu trúc từ asset

1. Mở `StructureTree` asset trong Inspector.
2. Chỉnh `Root Path` (nơi sẽ tạo cây thư mục).
3. Chỉnh cây `root`:
   - Đổi tên node, thêm/bớt children (`+` / `−`).
   - Gắn asmdef cho node cần (`+a` → nhập Assembly name + References).
4. Bấm **Create Structure**:
   - Tạo folders + `.asmdef` files (skip nếu đã tồn tại).
   - Folder tên `Editor` tự thêm `includePlatforms: ["Editor"]` vào asmdef.

### Ghi chú

- Bỏ qua thư mục bắt đầu bằng `.` khi export.
- `.asmdef` chỉ được ghi mới, không ghi đè file đã tồn tại.
- `StructureNode.children` và `AsmdefConfig` dùng `[SerializeReference]` cho phép nullable + cây lồng nhau.

### Dành cho AI Agent

- **Entry points**:
  - `StructureTree.CreateStructure()`: API chính để tạo folders + asmdefs.
  - Menu `Assets/Export Structure Tree`: tạo asset từ cấu trúc có sẵn.
- **Invariants**:
  - `root` là duy nhất. `rootPath` + `root` phải đồng bộ.
  - Chỉ tạo thư mục và `.asmdef`, không tạo/xóa file khác.
  - `WriteAsmdef` skip nếu file đã tồn tại (không overwrite).
- **Giới hạn**:
  - Chỉ hoạt động trong Editor (`UnityEditor` API).
  - Không xử lý symbolic link.
