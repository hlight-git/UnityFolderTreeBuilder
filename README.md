## Unity Folder Tree Builder

`UnityFolderTreeBuilder` cung cấp một `ScriptableObject` (`FolderTree`) và custom editor để:

- **Export** cấu trúc thư mục hiện có trong Project thành một `FolderTree` asset.
- **Tạo lại** (re-create) cấu trúc thư mục từ một `FolderTree` asset.

### Cấu trúc chính

- `FolderTree.cs`
  - `rootPath` (`string`): đường dẫn thư mục cha, nơi sẽ tạo các folder con (ví dụ: `Assets/Submodules`).
  - `root` (`FolderNode`): node root duy nhất, tên chính là tên thư mục gốc (ví dụ: `CompositeTask`), bên dưới là `children`.
  - `CreateFolders()`: tạo lại cây thư mục bắt đầu từ `rootPath` và `root`.
  - Menu:
    - **Assets/Export Folder Tree**: export một folder trong Project thành `FolderTree` asset.
      - Hỗ trợ:
        - Chọn folder bên panel phải.
        - Right-click folder ở cây folder bên trái (dùng `ProjectWindowUtil.GetActiveFolderPath` qua reflection).

- `FolderTreeEditor.cs`
  - Custom inspector:
    - Hiển thị và cho phép chỉnh `rootPath`.
    - Vẽ cây `FolderNode` (một root, nhiều children lồng nhau).
    - Nút **Create Folders** để gọi `CreateFolders()`.

### Cách export FolderTree từ Project

1. Trong Project window, chọn **một folder**:
   - Chọn ở panel phải **hoặc**
   - Right-click trực tiếp folder ở cây bên trái.
2. Vào menu **Assets → Export Folder Tree**.
3. Chọn nơi lưu và tên file `FolderTree` asset (mặc định: `[FolderName]FolderTree.asset`).
4. Asset tạo ra sẽ:
   - `rootPath` = thư mục cha của folder bạn chọn.
   - `root` = cây `FolderNode` bắt đầu từ folder đó trở xuống.

### Cách dùng FolderTree để tạo lại cấu trúc thư mục

1. Mở `FolderTree` asset trong Inspector.
2. Chỉnh sửa cây `root` nếu cần:
   - Đổi tên node (tên folder).
   - Thêm/bớt child (`+` / `x`).
3. Đảm bảo `rootPath` trỏ tới nơi bạn muốn tạo cây thư mục (ví dụ: `Assets` hoặc `Assets/Submodules`).
4. Bấm nút **Create Folders**:
   - Tool sẽ tạo đủ các folder theo tree (bỏ qua các node `null` hoặc tên rỗng).
   - Có cơ chế tránh vòng lặp vô hạn trong dữ liệu (dùng `HashSet<FolderNode>`).

### Ghi chú

- Tool bỏ qua các thư mục bắt đầu bằng dấu chấm (`.`) khi export (ví dụ `.git`, `.vscode`).
- `FolderNode.children` là `List<FolderNode>` serialize bằng `SerializeReference`, cho phép cấu trúc cây lồng nhau linh hoạt.
- Khi thay đổi cấu trúc `FolderTree` (ví dụ từ `roots` → `root`), các asset cũ có thể không tương thích, nên nên export lại từ folder thật nếu cần.

### Dành cho AI Agent

- **Entry points quan trọng**:
  - `FolderTree.CreateFolders()` là API chính để tạo thư mục từ asset.
  - Menu `Assets/Export Folder Tree` là nơi khởi tạo asset từ cấu trúc thư mục có sẵn.
- **Invariants**:
  - `root` là **duy nhất**, không dùng danh sách `roots`.
  - `rootPath` + cây `root` phải luôn đồng bộ: các folder được tạo dưới `rootPath`, bắt đầu từ `root.name`.
  - Không tạo/thao tác file, chỉ tạo thư mục (`Directory.CreateDirectory`).
- **Giới hạn**:
  - Chỉ hoạt động trong Editor (các API `UnityEditor`).
  - Không xử lý symbolic link, chỉ thư mục thật trong Project.

